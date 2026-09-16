using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Services;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Security.Claims;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class JobsController : ControllerBase
    {
        private readonly IHangfireJobService _jobService;
        private readonly ITenantProvider _tenantProvider;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;
        private readonly ICentralDbContext _centralDbContext;
        private readonly ISmartDbContext _smartDbContext;
        private readonly IConfiguration _configuration;

        public JobsController(
            IHangfireJobService jobService,
            ITenantProvider tenantProvider,
            TenantHangfireRuntime tenantHangfireRuntime,
            ICentralDbContext centralDbContext,
            ISmartDbContext smartDbContext,
            IConfiguration configuration)
        {
            _jobService = jobService;
            _tenantProvider = tenantProvider;
            _tenantHangfireRuntime = tenantHangfireRuntime;
            _centralDbContext = centralDbContext;
            _smartDbContext = smartDbContext;
            _configuration = configuration;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IReadOnlyList<HangfireJobSnapshotDto>>> GetRecentJobs([FromQuery] int take = 50)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var pageSize = Math.Clamp(take, 1, 200);

            try
            {
                if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                {
                    return StatusCode(503, new
                    {
                        Message = $"No se pudo consultar el estado de Hangfire para el tenant '{tenant.CompanyKey}'. Verifique que la base de datos sea accesible.",
                        Detail = "El tenant actual no tiene una cadena de conexión resuelta."
                    });
                }

                var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                await using var connection = new SqlConnection(sqlConnectionString);
                await connection.OpenAsync(HttpContext.RequestAborted);
                var snapshots = await QueryRecentHangfireJobsAsync(connection, pageSize, HttpContext.RequestAborted);
                
                // Enriquecer con el código de sección
                var sectionIds = snapshots.Where(s => s.IdSeccion.HasValue).Select(s => s.IdSeccion!.Value).Distinct().ToList();
                if (sectionIds.Any())
                {
                    var sectionCodes = await _smartDbContext.SeccionTable
                        .Where(s => sectionIds.Contains(s.IdSeccion))
                        .Select(s => new { s.IdSeccion, s.Codigo })
                        .ToDictionaryAsync(s => s.IdSeccion, s => s.Codigo, HttpContext.RequestAborted);

                    foreach (var snapshot in snapshots)
                    {
                        snapshot.CompanyKey = tenant.CompanyKey;
                        if (snapshot.IdSeccion.HasValue && sectionCodes.TryGetValue(snapshot.IdSeccion.Value, out var code))
                        {
                            snapshot.SectionCode = code;
                        }
                    }
                }
                else
                {
                    foreach (var snapshot in snapshots)
                    {
                        snapshot.CompanyKey = tenant.CompanyKey;
                    }
                }

                return Ok(snapshots);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(503, new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { 
                    Message = $"No se pudo consultar el estado de Hangfire para el tenant '{tenant.CompanyKey}'. Verifique que la base de datos sea accesible.",
                    Detail = ex.Message 
                });
            }

        }

        [HttpGet("by-id/{jobId}")]
        public async Task<ActionResult<HangfireJobSnapshotDto>> GetJobById(string jobId)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                {
                    return StatusCode(503, new
                    {
                        Message = $"No se pudo consultar el estado de Hangfire para el tenant '{tenant.CompanyKey}'. Verifique que la base de datos sea accesible.",
                        Detail = "El tenant actual no tiene una cadena de conexión resuelta."
                    });
                }

                var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                await using var connection = new SqlConnection(sqlConnectionString);
                await connection.OpenAsync(HttpContext.RequestAborted);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT
                        CONVERT(varchar(50), j.Id) AS JobId,
                        COALESCE(s.Name, j.StateName, 'Unknown') AS StateName,
                        j.InvocationData,
                        j.Arguments,
                        COALESCE(s.CreatedAt, j.CreatedAt) AS TimestampUtc,
                        s.Reason,
                        s.Data
                    FROM [HangFire].[Job] AS j
                    OUTER APPLY (
                        SELECT TOP (1)
                            st.Name,
                            st.Reason,
                            st.Data,
                            st.CreatedAt
                        FROM [HangFire].[State] AS st
                        WHERE st.JobId = j.Id
                        ORDER BY st.Id DESC
                    ) AS s
                    WHERE j.Id = @jobId;
                    """;
                command.Parameters.Add(new SqlParameter("@jobId", System.Data.SqlDbType.BigInt) { Value = long.Parse(jobId) });

                await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
                if (!await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    return NotFound(new { Message = $"No se encontro el job {jobId} en Hangfire." });
                }

                var args = ParseHangfireArguments(reader["Arguments"]?.ToString());
                int? idSeccion = null;
                if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
                {
                    idSeccion = parsedSection;
                }

                return Ok(new HangfireJobSnapshotDto
                {
                    JobId = reader["JobId"]?.ToString() ?? jobId,
                    CompanyKey = tenant.CompanyKey,
                    State = reader["StateName"]?.ToString() ?? "Unknown",
                    Method = ParseHangfireMethod(reader["InvocationData"]?.ToString()),
                    IdSeccion = idSeccion,
                    SectionCode = idSeccion.HasValue ? await _smartDbContext.SeccionTable.Where(s => s.IdSeccion == idSeccion.Value).Select(s => s.Codigo).FirstOrDefaultAsync(HttpContext.RequestAborted) : null,
                    Arguments = args,
                    Error = ParseHangfireError(reader["Reason"] as string, reader["Data"] as string),
                    Timestamp = reader["TimestampUtc"] is DateTime dt ? dt : DateTime.UtcNow
                });
            }
            catch (FormatException)
            {
                return BadRequest(new { Message = $"El jobId '{jobId}' no es válido." });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"No se pudo consultar el job {jobId} para el tenant '{tenant.CompanyKey}'.",
                    Detail = ex.Message
                });
            }
        }

        [HttpPost("{jobId}/requeue")]
        public async Task<ActionResult<object>> RequeueJob(string jobId)
        {
            var executedBy = GetManualExecutorName();
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                {
                    return StatusCode(503, new
                    {
                        Message = $"No se pudo re-encolar el job {jobId} para el tenant '{tenant.CompanyKey}'.",
                        Detail = "El tenant actual no tiene una cadena de conexión resuelta."
                    });
                }

                var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                await using var sqlConnection = new SqlConnection(sqlConnectionString);
                await sqlConnection.OpenAsync(HttpContext.RequestAborted);
                await using var command = sqlConnection.CreateCommand();
                command.CommandText = """
                    SELECT
                        CONVERT(varchar(50), j.Id) AS JobId,
                        COALESCE(s.Name, j.StateName, 'Unknown') AS StateName
                    FROM [HangFire].[Job] AS j
                    OUTER APPLY (
                        SELECT TOP (1)
                            st.Name
                        FROM [HangFire].[State] AS st
                        WHERE st.JobId = j.Id
                        ORDER BY st.Id DESC
                    ) AS s
                    WHERE j.Id = @jobId;
                    """;
                command.Parameters.Add(new SqlParameter("@jobId", System.Data.SqlDbType.BigInt) { Value = long.Parse(jobId) });

                await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
                if (!await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    return NotFound(new { Message = $"No se encontro el job {jobId} en Hangfire." });
                }

                var currentState = reader["StateName"]?.ToString() ?? string.Empty;
                if (!IsRequeueableState(currentState))
                {
                    return BadRequest(new
                    {
                        Message = $"El job {jobId} no se puede re-encolar desde el estado '{currentState}'."
                    });
                }

                var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                var client = new BackgroundJobClient(storage);
                var changed = client.ChangeState(jobId, new EnqueuedState(), currentState);
                if (!changed)
                {
                    return Conflict(new
                    {
                        Message = $"No se pudo re-encolar el job {jobId}. El estado pudo haber cambiado."
                    });
                }

                return Ok(new
                {
                    JobId = jobId,
                    Message = $"Job {jobId} re-encolado correctamente por {executedBy}."
                });
            }
            catch (FormatException)
            {
                return BadRequest(new { Message = $"El jobId '{jobId}' no es válido." });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"No se pudo re-encolar el job {jobId} para el tenant '{tenant.CompanyKey}'.",
                    Detail = ex.Message
                });
            }
        }

        [HttpDelete("{jobId}")]
        public async Task<ActionResult<object>> DeleteJob(string jobId)
        {
            var executedBy = GetManualExecutorName();
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            try
            {
                var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                var client = new BackgroundJobClient(storage);
                
                var deleted = client.Delete(jobId);

                // Purga física directa del job en SQL Server para removerlo de la base de datos
                if (long.TryParse(jobId, out var jobIdLong))
                {
                    try
                    {
                        var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                        await using var sqlConnection = new SqlConnection(sqlConnectionString);
                        await sqlConnection.OpenAsync(HttpContext.RequestAborted);
                        await using var command = sqlConnection.CreateCommand();
                        command.CommandText = @"
                            DELETE FROM [HangFire].[JobParameter] WHERE JobId = @JobId;
                            DELETE FROM [HangFire].[State] WHERE JobId = @JobId;
                            DELETE FROM [HangFire].[Job] WHERE Id = @JobId;";
                        command.Parameters.Add(new SqlParameter("@JobId", System.Data.SqlDbType.BigInt) { Value = jobIdLong });
                        await command.ExecuteNonQueryAsync(HttpContext.RequestAborted);
                    }
                    catch (Exception)
                    {
                    }
                }

                return Ok(new
                {
                    Message = $"El job {jobId} ha sido detenido/eliminado."
                });
            }
            catch (FormatException)
            {
                return BadRequest(new { Message = $"El jobId '{jobId}' no es válido." });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"Error al intentar detener el job {jobId}.",
                    Detail = ex.Message
                });
            }
        }

        [HttpDelete("company-data")]
        public async Task<ActionResult<object>> PurgeCompanyData()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            int deletedRecurringJobs;
            int deletedJobs;
            try
            {
                if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                {
                    return StatusCode(503, new
                    {
                        Message = $"No se pudo limpiar los datos de Hangfire para el tenant '{tenant.CompanyKey}'.",
                        Detail = "El tenant actual no tiene una cadena de conexión resuelta."
                    });
                }

                var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                var client = new BackgroundJobClient(storage);
                var recurringManager = new RecurringJobManager(storage);

                var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                await using var sqlConnection = new SqlConnection(sqlConnectionString);
                await sqlConnection.OpenAsync(HttpContext.RequestAborted);

                var recurringIds = await QueryRecurringJobIdsAsync(sqlConnection, HttpContext.RequestAborted);
                deletedRecurringJobs = 0;
                foreach (var recurringJobId in recurringIds)
                {
                    try
                    {
                        recurringManager.RemoveIfExists(recurringJobId);
                        deletedRecurringJobs++;
                    }
                    catch { }
                }

                // Purga física de todas las tablas de registros de Hangfire en SQL Server
                await using var purgeCommand = sqlConnection.CreateCommand();
                purgeCommand.CommandText = @"
                    DELETE FROM [HangFire].[JobParameter];
                    DELETE FROM [HangFire].[State];
                    DELETE FROM [HangFire].[Set];
                    DELETE FROM [HangFire].[List];
                    DELETE FROM [HangFire].[Hash];
                    DELETE FROM [HangFire].[Job];";
                deletedJobs = await purgeCommand.ExecuteNonQueryAsync(HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"No se pudo limpiar los datos de Hangfire para el tenant '{tenant.CompanyKey}'.",
                    Detail = ex.Message
                });
            }

            var companyConfigId = await _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.CompanyKey == tenant.CompanyKey)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            var deletedScheduleExecutions = 0;
            var deletedSyncJobs = 0;
            if (companyConfigId != 0)
            {
                deletedScheduleExecutions = await _centralDbContext.SyncScheduleExecutions
                    .Where(e => e.CompanyConfigId == companyConfigId)
                    .ExecuteDeleteAsync(HttpContext.RequestAborted);

                deletedSyncJobs = await _centralDbContext.SyncJobs
                    .Where(j => j.CompanyKey == tenant.CompanyKey)
                    .ExecuteDeleteAsync(HttpContext.RequestAborted);
            }

            var deletedLogs = await _smartDbContext.Set<APITeamsV3.Domain.Entities.TeamsLogOperativo>()
                .ExecuteDeleteAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                CompanyKey = tenant.CompanyKey,
                DeletedRecurringJobs = deletedRecurringJobs,
                DeletedHangfireJobs = deletedJobs,
                DeletedLogs = deletedLogs,
                DeletedSyncJobs = deletedSyncJobs,
                DeletedScheduleExecutions = deletedScheduleExecutions,
                Message = $"Se eliminaron jobs y logs del tenant {tenant.CompanyKey}."
            });
        }

        [HttpGet("recurring")]
        public async Task<ActionResult<IReadOnlyList<HangfireRecurringJobSnapshotDto>>> GetRecurringJobs()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
                {
                    return StatusCode(503, new
                    {
                        Message = $"No se pudo consultar los jobs recurrentes de Hangfire para el tenant '{tenant.CompanyKey}'. Verifique que la base de datos sea accesible.",
                        Detail = "El tenant actual no tiene una cadena de conexión resuelta."
                    });
                }

                var sqlConnectionString = await ResolveTenantSqlConnectionStringAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                await using var connection = new SqlConnection(sqlConnectionString);
                await connection.OpenAsync(HttpContext.RequestAborted);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    WITH RecurringHashes AS (
                        SELECT
                            s.Value AS RecurringJobId,
                            s.Score AS SetScore,
                            MAX(CASE WHEN h.Field = 'Cron' THEN h.Value END) AS Cron,
                            MAX(CASE WHEN h.Field = 'Queue' THEN h.Value END) AS Queue,
                            MAX(CASE WHEN h.Field = 'Job' THEN h.Value END) AS JobPayload,
                            MAX(CASE WHEN h.Field = 'TimeZoneId' THEN h.Value END) AS TimeZoneId,
                            MAX(CASE WHEN h.Field = 'CreatedAt' THEN h.Value END) AS CreatedAt,
                            MAX(CASE WHEN h.Field = 'LastExecution' THEN h.Value END) AS LastExecution,
                            MAX(CASE WHEN h.Field = 'NextExecution' THEN h.Value END) AS NextExecution,
                            MAX(CASE WHEN h.Field = 'LastJobId' THEN h.Value END) AS LastJobId,
                            MAX(CASE WHEN h.Field = 'LastJobState' THEN h.Value END) AS LastJobState,
                            MAX(CASE WHEN h.Field = 'Error' THEN h.Value END) AS ErrorText,
                            MAX(CASE WHEN h.Field = 'Removed' THEN h.Value END) AS Removed
                        FROM [HangFire].[Set] AS s
                        INNER JOIN [HangFire].[Hash] AS h
                            ON h.[Key] = CONCAT('recurring-job:', s.Value)
                        WHERE s.[Key] = 'recurring-jobs'
                        GROUP BY s.Value, s.Score
                    )
                    SELECT
                        rh.RecurringJobId,
                        rh.Cron,
                        rh.Queue,
                        rh.JobPayload,
                        rh.TimeZoneId,
                        rh.CreatedAt,
                        rh.LastExecution,
                        rh.NextExecution,
                        rh.LastJobId,
                        rh.LastJobState,
                        rh.ErrorText,
                        rh.Removed,
                        rh.SetScore,
                        js.RealLastJobState,
                        js.RealLastExecution,
                        js.Reason AS LastJobReason,
                        js.Data AS LastJobStateData
                    FROM RecurringHashes AS rh
                    OUTER APPLY (
                        SELECT TOP (1)
                            st.Name AS RealLastJobState,
                            st.CreatedAt AS RealLastExecution,
                            st.Reason,
                            st.Data
                        FROM [HangFire].[Job] AS j
                        INNER JOIN [HangFire].[State] AS st ON st.Id = j.StateId
                        WHERE CONVERT(varchar(50), j.Id) = rh.LastJobId
                    ) AS js
                    ORDER BY
                        COALESCE(TRY_CONVERT(datetime2, rh.CreatedAt), TRY_CONVERT(datetime2, rh.LastExecution), TRY_CONVERT(datetime2, rh.NextExecution), '1900-01-01') DESC,
                        rh.RecurringJobId DESC;
                    """;

                var recurringJobs = new List<HangfireRecurringJobSnapshotDto>();
                await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
                while (await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    var jobPayload = reader["JobPayload"]?.ToString();
                    var args = ParseHangfireArgumentsFromRecurringJob(jobPayload);
                    int? idSeccion = null;
                    if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
                    {
                        idSeccion = parsedSection;
                    }

                    var error = reader["ErrorText"]?.ToString() ?? string.Empty;
                    var lastExecutionFromHash = ParseHangfireDateTime(reader["LastExecution"]?.ToString());
                    var realLastExecution = reader["RealLastExecution"] is DateTime dtExecution ? dtExecution : (DateTime?)null;
                    
                    var lastJobStateFromHash = reader["LastJobState"]?.ToString() ?? string.Empty;
                    var realLastJobState = reader["RealLastJobState"]?.ToString() ?? string.Empty;
                    var effectiveState = !string.IsNullOrWhiteSpace(realLastJobState) ? realLastJobState : lastJobStateFromHash;

                    var nextExecutionFromHash = ParseHangfireDateTime(reader["NextExecution"]?.ToString());
                    var scoreValue = reader["SetScore"] != DBNull.Value ? Convert.ToDouble(reader["SetScore"]) : 0;
                    var nextExecutionFromScore = scoreValue > 0 
                        ? (scoreValue > 2000000000 ? DateTimeOffset.FromUnixTimeMilliseconds((long)scoreValue).DateTime : DateTimeOffset.FromUnixTimeSeconds((long)scoreValue).DateTime)
                        : (DateTime?)null;

                    recurringJobs.Add(new HangfireRecurringJobSnapshotDto
                    {
                        Id = reader["RecurringJobId"]?.ToString() ?? string.Empty,
                        CompanyKey = tenant.CompanyKey,
                        Cron = reader["Cron"]?.ToString() ?? string.Empty,
                        Queue = reader["Queue"]?.ToString() ?? "default",
                        Method = ParseHangfireMethod(jobPayload),
                        IdSeccion = idSeccion,
                        SectionCode = idSeccion.HasValue ? await _smartDbContext.SeccionTable.Where(s => s.IdSeccion == idSeccion.Value).Select(s => s.Codigo).FirstOrDefaultAsync(HttpContext.RequestAborted) : null,
                        CreatedAt = ParseHangfireDateTime(reader["CreatedAt"]?.ToString()),
                        LastExecution = realLastExecution ?? lastExecutionFromHash,
                        NextExecution = nextExecutionFromScore ?? nextExecutionFromHash,
                        LastJobId = reader["LastJobId"]?.ToString() ?? string.Empty,
                        LastJobState = effectiveState,
                        LastResult = string.IsNullOrWhiteSpace(error)
                            ? ParseHangfireError(reader["LastJobReason"] as string, reader["LastJobStateData"] as string) 
                              ?? (effectiveState == "Succeeded" ? "Ejecucion completada correctamente." : string.Empty)
                            : error,
                        TimeZoneId = reader["TimeZoneId"]?.ToString() ?? string.Empty,
                        Error = error,
                        Removed = bool.TryParse(reader["Removed"]?.ToString(), out var removed) && removed
                    });
                }

                return Ok(recurringJobs);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"No se pudo consultar los jobs recurrentes de Hangfire para el tenant '{tenant.CompanyKey}'. Verifique que la base de datos sea accesible.",
                    Detail = ex.Message
                });
            }
        }

        [HttpPost("recordings-transfer-pilot/run")]
        public async Task<ActionResult<object>> RunPilotRecordingTransfersNow()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueuePilotRecordingTransfers(tenant.CompanyKey, executedBy);
            return Ok(new
            {
                JobId = jobId,
                Message = $"Recording transfer piloto encolado para tenant {tenant.CompanyKey} por {executedBy}."
            });
        }

        [HttpPost("recordings-transfer/{idSeccion}")]
        public async Task<ActionResult<object>> RunRecordingTransferForSectionNow(int idSeccion)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueRecordingTransferForSection(idSeccion, tenant.CompanyKey, executedBy);
            return Ok(new
            {
                JobId = jobId,
                Message = $"Recording transfer para la sección {idSeccion} encolado por {executedBy}."
            });
        }

        [HttpGet("recordings-transfer-pilot/config")]
        public async Task<ActionResult<RecordingTransferPilotJobConfigDto>> GetPilotRecordingTransferConfig()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var company = await _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Include(c => c.PilotSections)
                .FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey, HttpContext.RequestAborted);

            if (company == null)
            {
                return NotFound(new { Message = $"No se encontro configuracion para el tenant '{tenant.CompanyKey}'." });
            }

            var fallbackCron = ResolveGlobalRecordingTransferCron(_configuration);
            var effectiveCron = string.IsNullOrWhiteSpace(company.RecordingTransferCron)
                ? fallbackCron
                : company.RecordingTransferCron.Trim();

            return Ok(new RecordingTransferPilotJobConfigDto
            {
                IsEnabled = company.IsRecordingTransferJobEnabled,
                Cron = effectiveCron,
                TimeZoneId = company.TimeZoneId,
                IsPilotMode = company.IsPilotMode,
                PilotSectionsConfigured = company.PilotSections.Count
            });
        }

        [HttpPut("recordings-transfer-pilot/config")]
        public async Task<ActionResult<RecordingTransferPilotJobConfigDto>> UpdatePilotRecordingTransferConfig(
            [FromBody] UpdateRecordingTransferPilotJobConfigRequest request)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var company = await _centralDbContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey, HttpContext.RequestAborted);

            if (company == null)
            {
                return NotFound(new { Message = $"No se encontro configuracion para el tenant '{tenant.CompanyKey}'." });
            }

            if (request.IsEnabled && string.IsNullOrWhiteSpace(request.Cron))
            {
                return BadRequest(new { Message = "Cron es requerido cuando el job esta habilitado." });
            }

            if (!string.IsNullOrWhiteSpace(request.Cron) && !LooksLikeValidCron(request.Cron))
            {
                return BadRequest(new
                {
                    Message = "Cron invalido. Use formato de 5 campos (por ejemplo: '*/15 * * * *') o expresiones tipo '@hourly'."
                });
            }

            var normalizedCron = string.IsNullOrWhiteSpace(request.Cron)
                ? null
                : request.Cron.Trim();

            company.IsRecordingTransferJobEnabled = request.IsEnabled;
            company.RecordingTransferCron = normalizedCron;
            await _centralDbContext.SaveChangesAsync(HttpContext.RequestAborted);

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            var recurringManager = new RecurringJobManager(storage);

            const string recurringJobId = "recordings-transfer-pilot";
            if (!company.IsPilotMode || company.PilotSections.Count == 0 || !company.IsRecordingTransferJobEnabled)
            {
                recurringManager.RemoveIfExists(recurringJobId);
            }
            else
            {
                var effectiveCron = string.IsNullOrWhiteSpace(company.RecordingTransferCron)
                    ? ResolveGlobalRecordingTransferCron(_configuration)
                    : company.RecordingTransferCron.Trim();

                recurringManager.AddOrUpdate<HangfireJobService>(
                    recurringJobId,
                    service => service.RunPilotRecordingTransfers(company.CompanyKey, null),
                    effectiveCron,
                    new RecurringJobOptions
                    {
                        TimeZone = ResolveTimeZone(company.TimeZoneId)
                    });
            }

            return Ok(new RecordingTransferPilotJobConfigDto
            {
                IsEnabled = company.IsRecordingTransferJobEnabled,
                Cron = string.IsNullOrWhiteSpace(company.RecordingTransferCron)
                    ? ResolveGlobalRecordingTransferCron(_configuration)
                    : company.RecordingTransferCron.Trim(),
                TimeZoneId = company.TimeZoneId,
                IsPilotMode = company.IsPilotMode,
                PilotSectionsConfigured = company.PilotSections.Count
            });
        }

        private static string BuildRecurringJobResult(RecurringJobDto recurringJob, JobDetailsDto? jobDetails)
        {
            if (!string.IsNullOrWhiteSpace(recurringJob.Error))
            {
                return recurringJob.Error;
            }

            if (jobDetails == null)
            {
                return string.IsNullOrWhiteSpace(recurringJob.LastJobState)
                    ? "Aun sin ejecuciones."
                    : recurringJob.LastJobState;
            }

            var latestState = jobDetails.History?
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (latestState == null)
            {
                return string.IsNullOrWhiteSpace(recurringJob.LastJobState)
                    ? "Sin historial disponible."
                    : recurringJob.LastJobState;
            }

            if (latestState.Data != null)
            {
                if (latestState.Data.TryGetValue("ExceptionMessage", out var exceptionMessage) &&
                    !string.IsNullOrWhiteSpace(exceptionMessage))
                {
                    return exceptionMessage;
                }

                if (latestState.Data.TryGetValue("Result", out var result) &&
                    !string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(latestState.Reason))
            {
                return latestState.Reason;
            }

            return latestState.StateName switch
            {
                "Succeeded" => "Ejecucion completada correctamente.",
                "Failed" => "La ultima ejecucion fallo.",
                "Deleted" => "La ultima ejecucion fue eliminada.",
                "Scheduled" => "La ejecucion quedo programada.",
                "Processing" => "La ultima ejecucion sigue en proceso.",
                _ => latestState.StateName
            };
        }

        private static bool IsRequeueableState(string state)
        {
            return string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Deleted", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ResolveTenantSqlConnectionStringAsync(string companyKey, CancellationToken cancellationToken)
        {
            return await _tenantHangfireRuntime.GetSqlConnectionStringAsync(companyKey, cancellationToken);
        }

        private static string ResolveGlobalRecordingTransferCron(IConfiguration configuration)
        {
            var configured = configuration["Hangfire:RecordingTransferCron"];
            return string.IsNullOrWhiteSpace(configured) ? Cron.Hourly() : configured.Trim();
        }

        private static bool LooksLikeValidCron(string cron)
        {
            var value = cron.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith("@", StringComparison.Ordinal))
            {
                return value is "@yearly" or "@annually" or "@monthly" or "@weekly" or "@daily" or "@hourly";
            }

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 5;
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch
                {
                }
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static async Task<List<HangfireJobSnapshotDto>> QueryRecentHangfireJobsAsync(
            SqlConnection connection,
            int take,
            CancellationToken cancellationToken)
        {
            await TryEnsureRecentJobsStoredProcedureAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.CommandText = "dbo.cTeamsRecentHangfireJobs";
            command.Parameters.Add(new SqlParameter("@Take", System.Data.SqlDbType.Int) { Value = take });

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await ReadHangfireJobSnapshotsAsync(reader, take, cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 2812)
            {
                command.Parameters.Clear();
                command.CommandType = System.Data.CommandType.Text;
                command.CommandText = """
                    SELECT TOP (@take)
                        CONVERT(varchar(50), j.Id) AS JobId,
                        COALESCE(s.Name, j.StateName, 'Unknown') AS StateName,
                        j.InvocationData,
                        j.Arguments,
                        COALESCE(s.CreatedAt, j.CreatedAt) AS TimestampUtc,
                        s.Reason,
                        s.Data
                    FROM [HangFire].[Job] AS j
                    OUTER APPLY (
                        SELECT TOP (1)
                            st.Name,
                            st.Reason,
                            st.Data,
                            st.CreatedAt
                        FROM [HangFire].[State] AS st
                        WHERE st.JobId = j.Id
                        ORDER BY st.Id DESC
                    ) AS s
                    WHERE COALESCE(s.Name, j.StateName, 'Unknown') <> 'Deleted'
                    ORDER BY 
                        CASE 
                            WHEN COALESCE(s.Name, j.StateName, 'Unknown') = 'Processing' THEN 1
                            WHEN COALESCE(s.Name, j.StateName, 'Unknown') IN ('Enqueued', 'Scheduled', 'Awaiting') THEN 2
                            WHEN COALESCE(s.Name, j.StateName, 'Unknown') = 'Failed' THEN 3
                            ELSE 4
                        END ASC,
                        COALESCE(s.CreatedAt, j.CreatedAt) DESC, 
                        j.Id DESC;
                    """;
                command.Parameters.Add(new SqlParameter("@take", System.Data.SqlDbType.Int) { Value = take });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await ReadHangfireJobSnapshotsAsync(reader, take, cancellationToken);
            }
        }

        private static async Task TryEnsureRecentJobsStoredProcedureAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = """
                IF OBJECT_ID(N'dbo.cTeamsRecentHangfireJobs', N'P') IS NULL
                    EXEC('CREATE PROCEDURE dbo.cTeamsRecentHangfireJobs AS BEGIN SET NOCOUNT ON; SELECT 1 AS [JobId], ''Unknown'' AS [StateName], NULL AS [InvocationData], NULL AS [Arguments], GETUTCDATE() AS [TimestampUtc], NULL AS [Reason], NULL AS [Data]; END');
                EXEC('
                ALTER PROCEDURE dbo.cTeamsRecentHangfireJobs
                    @Take INT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT TOP (@Take)
                        CONVERT(varchar(50), j.Id) AS JobId,
                        COALESCE(s.Name, j.StateName, ''Unknown'') AS StateName,
                        j.InvocationData,
                        j.Arguments,
                        COALESCE(s.CreatedAt, j.CreatedAt) AS TimestampUtc,
                        s.Reason,
                        s.Data
                    FROM [HangFire].[Job] AS j
                    OUTER APPLY (
                        SELECT TOP (1)
                            st.Name,
                            st.Reason,
                            st.Data,
                            st.CreatedAt
                        FROM [HangFire].[State] AS st
                        WHERE st.JobId = j.Id
                        ORDER BY st.Id DESC
                    ) AS s
                    WHERE COALESCE(s.Name, j.StateName, ''Unknown'') <> ''Deleted''
                    ORDER BY 
                        CASE 
                            WHEN COALESCE(s.Name, j.StateName, ''Unknown'') = ''Processing'' THEN 1
                            WHEN COALESCE(s.Name, j.StateName, ''Unknown'') IN (''Enqueued'', ''Scheduled'', ''Awaiting'') THEN 2
                            WHEN COALESCE(s.Name, j.StateName, ''Unknown'') = ''Failed'' THEN 3
                            ELSE 4
                        END ASC,
                        COALESCE(s.CreatedAt, j.CreatedAt) DESC, 
                        j.Id DESC;
                END
                ');
                """;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 229 || ex.Number == 262 || ex.Number == 2760 || ex.Number == 15151)
            {
                // Fallback to inline query when the app login cannot create/alter procedures.
            }
        }

        private static async Task<List<HangfireJobSnapshotDto>> ReadHangfireJobSnapshotsAsync(
            SqlDataReader reader,
            int capacity,
            CancellationToken cancellationToken)
        {
            var snapshots = new List<HangfireJobSnapshotDto>(capacity);
            while (await reader.ReadAsync(cancellationToken))
            {
                var jobId = reader["JobId"]?.ToString() ?? string.Empty;
                var state = reader["StateName"]?.ToString() ?? "Unknown";
                var invocationData = reader["InvocationData"]?.ToString();
                var argumentsData = reader["Arguments"]?.ToString();
                var reason = reader["Reason"] as string;
                var stateData = reader["Data"] as string;
                var timestamp = reader["TimestampUtc"] is DateTime dt ? dt : DateTime.UtcNow;

                var args = ParseHangfireArguments(argumentsData);
                int? idSeccion = null;
                if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
                {
                    idSeccion = parsedSection;
                }

                var method = ParseHangfireMethod(invocationData);
                var error = ParseHangfireError(reason, stateData);
                if (string.IsNullOrWhiteSpace(error) && state == "Succeeded")
                {
                    error = method == "RunPilotRecordingTransfers" 
                        ? $"Transferencia piloto completada para {args.FirstOrDefault() ?? "el tenant"}." 
                        : "Ejecucion completada correctamente.";
                }

                snapshots.Add(new HangfireJobSnapshotDto
                {
                    JobId = jobId,
                    State = state,
                    Method = method,
                    IdSeccion = idSeccion,
                    Arguments = args,
                    Error = error,
                    Timestamp = timestamp
                });
            }

            return snapshots;
        }

        private static string ParseHangfireMethod(string? invocationData)
        {
            if (string.IsNullOrWhiteSpace(invocationData))
            {
                return "Unknown";
            }

            try
            {
                using var document = JsonDocument.Parse(invocationData);
                if (TryGetJsonProperty(document.RootElement, out var methodElement, "Method", "method", "m"))
                {
                    var method = methodElement.GetString();
                    if (!string.IsNullOrWhiteSpace(method))
                    {
                        return method;
                    }
                }
            }
            catch
            {
            }

            return "Unknown";
        }

        private static string[] ParseHangfireArguments(string? argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Array.Empty<string>();
            }

            try
            {
                using var document = JsonDocument.Parse(argumentsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<string>();
                }

                return document.RootElement
                    .EnumerateArray()
                    .Select(static element => element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString() ?? string.Empty,
                        JsonValueKind.Number => element.ToString(),
                        JsonValueKind.True => bool.TrueString,
                        JsonValueKind.False => bool.FalseString,
                        JsonValueKind.Null => string.Empty,
                        _ => element.ToString()
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string? ParseHangfireError(string? reason, string? stateDataJson)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            if (string.IsNullOrWhiteSpace(stateDataJson))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(stateDataJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (document.RootElement.TryGetProperty("ExceptionMessage", out var exceptionMessage))
                {
                    return exceptionMessage.GetString();
                }

                if (document.RootElement.TryGetProperty("Result", out var result))
                {
                    return result.GetString();
                }
            }
            catch
            {
            }

            return null;
        }

        private static async Task<List<string>> QueryRecurringJobIdsAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT s.Value
                FROM [HangFire].[Set] AS s
                WHERE s.[Key] = 'recurring-jobs'
                  AND s.Value IS NOT NULL
                  AND s.Value <> '';
                """;

            var result = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static async Task<List<string>> QueryHangfireJobIdsAsync(SqlConnection connection, int take, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (@take) CONVERT(varchar(50), j.Id) AS JobId
                FROM [HangFire].[Job] AS j
                ORDER BY j.Id DESC;
                """;
            command.Parameters.Add(new SqlParameter("@take", System.Data.SqlDbType.Int) { Value = take });

            var result = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = reader["JobId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static string[] ParseHangfireArgumentsFromRecurringJob(string? recurringJobJson)
        {
            if (string.IsNullOrWhiteSpace(recurringJobJson))
            {
                return Array.Empty<string>();
            }

            try
            {
                using var document = JsonDocument.Parse(recurringJobJson);
                if (TryGetJsonProperty(document.RootElement, out var argsElement, "Args", "args", "a"))
                {
                    return ParseHangfireArguments(argsElement.GetRawText());
                }

                if (TryGetJsonProperty(document.RootElement, out var invocationElement, "InvocationData", "invocationData"))
                {
                    using var nestedDocument = JsonDocument.Parse(invocationElement.GetString() ?? invocationElement.GetRawText());
                    if (TryGetJsonProperty(nestedDocument.RootElement, out var nestedArgsElement, "Args", "args", "a"))
                    {
                        return ParseHangfireArguments(nestedArgsElement.GetRawText());
                    }
                }
            }
            catch
            {
            }

            return Array.Empty<string>();
        }

        private static bool TryGetJsonProperty(JsonElement element, out JsonElement value, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static DateTime? ParseHangfireDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, out var parsed))
            {
                return parsed;
            }

            if (long.TryParse(value, out var ticks))
            {
                try
                {
                    // Case 1: Standard Unix milliseconds (e.g. 1713435120000)
                    if (ticks > 1000000000000L && ticks < 3000000000000L)
                    {
                        return DateTimeOffset.FromUnixTimeMilliseconds(ticks).DateTime;
                    }
                    // Case 2: Standard Unix seconds (e.g. 1713435120)
                    if (ticks > 1000000000L && ticks < 3000000000L)
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(ticks).DateTime;
                    }
                    // Case 3: .NET Ticks (very large number)
                    if (ticks > 600000000000000000L)
                    {
                        return new DateTime(ticks, DateTimeKind.Utc);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static int DeleteJobsBatch(
            IEnumerable<string> jobIds,
            BackgroundJobClient client)
        {
            var deleted = 0;
            foreach (var jobId in jobIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (client.Delete(jobId))
                    {
                        deleted++;
                    }
                }
                catch
                {
                }
            }

            return deleted;
        }

        [HttpPost("generate-schedule/{idSeccion}")]
        public async Task<ActionResult<string>> GenerateSchedule(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueGenerateSchedule(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Generate Schedule Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-roster/{idSeccion}")]
        public async Task<ActionResult<string>> SyncRoster(int idSeccion, [FromQuery] bool fullSync = true)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncRoster(idSeccion, fullSync, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sincronizacion Operativa Completa encolada por {executedBy}" });
        }

        [HttpPost("sync-dates/{idSeccion}")]
        public async Task<ActionResult<string>> SyncDates(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncDates(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Dates Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-facilitator/{idSeccion}")]
        public async Task<ActionResult<string>> SyncFacilitator(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncFacilitator(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Facilitator Job Enqueued by {executedBy}" });
        }

        [HttpPost("update-join-url")]
        public async Task<ActionResult<string>> UpdateJoinUrl([FromBody] UpdateJoinUrlRequest request)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueUpdateJoinUrl(request.IdSeccion, request.JoinUrl, request.IdEvento, executedBy);
            return Ok(new { JobId = jobId, Message = $"Update Join URL Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-missing-students/{idSeccion}")]
        public async Task<ActionResult<string>> SyncMissingStudents(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncMissingStudents(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Missing Students Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-obsolete-students/{idSeccion}")]
        public async Task<ActionResult<string>> SyncObsoleteStudents(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncObsoleteStudents(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Obsolete Students Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-renamed-teams/{idSeccion}")]
        public async Task<ActionResult<string>> SyncRenamedTeams(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncRenamedTeams(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Renamed Teams Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-attendance/{idSeccion}")]
        public async Task<ActionResult<string>> SyncAttendance(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncAttendance(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Attendance Job Enqueued by {executedBy}" });
        }

        [HttpPost("sync-section-team/{idSeccion}")]
        public async Task<ActionResult<string>> SyncSectionTeam(int idSeccion)
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueSyncSectionTeam(idSeccion, executedBy);
            return Ok(new { JobId = jobId, Message = $"Sync Section Team Job Enqueued by {executedBy}" });
        }

        [HttpPost("check-storage-quota")]
        public async Task<ActionResult<string>> CheckStorageQuota()
        {
            var executedBy = GetManualExecutorName();
            var jobId = await _jobService.EnqueueCheckStorageQuota(executedBy);
            return Ok(new { JobId = jobId, Message = $"Storage Quota Check Job Enqueued by {executedBy}" });
        }

        [HttpGet("stats")]
        public async Task<ActionResult<HangfireStatsDto>> GetJobStats()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            try
            {
                var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
                var monitoringApi = storage.GetMonitoringApi();
                var statistics = monitoringApi.GetStatistics();

                return Ok(new HangfireStatsDto
                {
                    Enqueued = statistics.Enqueued,
                    Processing = statistics.Processing,
                    Succeeded = statistics.Succeeded,
                    Failed = statistics.Failed,
                    Scheduled = statistics.Scheduled,
                    Deleted = statistics.Deleted,
                    Recurring = statistics.Recurring
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    Message = $"No se pudieron consultar las estadísticas de Hangfire para el tenant '{tenant.CompanyKey}'.",
                    Detail = ex.Message
                });
            }
        }
        private string GetManualExecutorName()
        {
            var claims = User?.Claims;
            if (claims == null)
            {
                return "desconocido";
            }

            return claims.FirstOrDefault(c => c.Type == "name")?.Value
                   ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                   ?? claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                   ?? claims.FirstOrDefault(c => c.Type == "upn")?.Value
                   ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                   ?? claims.FirstOrDefault(c => c.Type == "unique_name")?.Value
                   ?? "desconocido";
        }
    }

    public class HangfireStatsDto
    {
        public long Enqueued { get; set; }
        public long Processing { get; set; }
        public long Succeeded { get; set; }
        public long Failed { get; set; }
        public long Scheduled { get; set; }
        public long Deleted { get; set; }
        public long Recurring { get; set; }
    }

    public class UpdateJoinUrlRequest
    {
        public int IdSeccion { get; set; }
        public string JoinUrl { get; set; } = string.Empty;
        public string IdEvento { get; set; } = string.Empty;
    }

    public class HangfireJobSnapshotDto
    {
        public string JobId { get; set; } = string.Empty;
        public string CompanyKey { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int? IdSeccion { get; set; }
        public string? SectionCode { get; set; }
        public DateTime Timestamp { get; set; }
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string? Error { get; set; }
    }
    
    public class HangfireRecurringJobSnapshotDto
    {
        public string Id { get; set; } = string.Empty;
        public string CompanyKey { get; set; } = string.Empty;
        public string Cron { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int? IdSeccion { get; set; }
        public string? SectionCode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastExecution { get; set; }
        public DateTime? NextExecution { get; set; }
        public string LastJobId { get; set; } = string.Empty;
        public string LastJobState { get; set; } = string.Empty;
        public string LastResult { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool Removed { get; set; }
    }

    public class RecordingTransferPilotJobConfigDto
    {
        public bool IsEnabled { get; set; }
        public string Cron { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public bool IsPilotMode { get; set; }
        public int PilotSectionsConfigured { get; set; }
    }

    public class UpdateRecordingTransferPilotJobConfigRequest
    {
        public bool IsEnabled { get; set; }
        public string? Cron { get; set; }
    }
}

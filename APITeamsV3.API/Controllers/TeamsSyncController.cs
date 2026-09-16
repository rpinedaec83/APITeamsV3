using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/sync")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsSyncController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly ICentralDbContext _centralDbContext;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;
        private readonly ILogger<TeamsSyncController> _logger;

        public TeamsSyncController(
            IMediator mediator,
            ITenantProvider tenantProvider,
            ICentralDbContext centralDbContext,
            TenantHangfireRuntime tenantHangfireRuntime,
            ILogger<TeamsSyncController> logger)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDbContext = centralDbContext;
            _tenantHangfireRuntime = tenantHangfireRuntime;
            _logger = logger;
        }

        /// <summary>
        /// Refreshes Smart programming tables and returns students
        /// that need to be added to the Teams group.
        /// </summary>
        [HttpPost("missing-students/{idSeccion}")]
        public async Task<ActionResult<List<MissingStudentDto>>> SyncMissingStudents(int idSeccion)
        {
            var result = await _mediator.Send(new SyncMissingStudentsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Returns students currently in the Teams group who are
        /// no longer enrolled in the section (to be removed).
        /// </summary>
        [HttpPost("obsolete-students/{idSeccion}")]
        public async Task<ActionResult<List<ObsoleteStudentDto>>> SyncObsoleteStudents(int idSeccion)
        {
            var result = await _mediator.Send(new SyncObsoleteStudentsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Returns teams whose display name / description no longer
        /// matches the Smart naming policy (to be renamed via Graph).
        /// </summary>
        [HttpPost("renamed-teams/{idSeccion}")]
        public async Task<ActionResult<List<RenamedTeamDto>>> SyncRenamedTeams(int idSeccion)
        {
            var result = await _mediator.Send(new SyncRenamedTeamsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Syncs facilitators for a section (delegates to existing SyncTeamFacilitatorsCommand).
        /// </summary>
        [HttpPost("facilitators/{idSeccion}")]
        public async Task<ActionResult<List<TeamFacilitatorChangeDto>>> SyncFacilitators(int idSeccion)
        {
            var result = await _mediator.Send(new SyncTeamFacilitatorsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Identifies teams whose section no longer exists in Smart table Seccion,
        /// soft-deletes them in database (EstadoTeam='I') and deletes them from Microsoft Teams via Graph.
        /// </summary>
        [HttpPost("obsolete-teams")]
        public async Task<ActionResult<SyncObsoleteTeamsResult>> SyncObsoleteTeams()
        {
            var result = await _mediator.Send(new SyncObsoleteTeamsCommand());
            return Ok(result);
        }

        [HttpPost("obsolete-teams/{idSeccion}")]
        public async Task<ActionResult<SyncObsoleteTeamsResult>> SyncObsoleteTeamBySection(int idSeccion)
        {
            var result = await _mediator.Send(new SyncObsoleteTeamsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Triggers full synchronization of all teams across all sections.
        /// Fetches all section IDs using Option 19 logic (filtered by SEDE),
        /// then enqueues chained Hangfire jobs per section to sync teams,
        /// students, facilitators, and meetings.
        /// </summary>
        [HttpPost("all")]
        public async Task<ActionResult<SyncAllTeamsResult>> SyncAll([FromQuery] string sede = "WI,SV,AP,AT,CH,IV,LN,PI,PT,SL,SM")
        {
            var result = await _mediator.Send(new SyncAllTeamsCommand(sede));
            return Ok(result);
        }

        [HttpPost("section/{idSeccion}")]
        public async Task<IActionResult> SyncSectionTeam(int idSeccion, [FromQuery] string companyKey = "idat")
        {
            // Opcionalmente se puede devolver Accepted() y encolar, pero para simplificar
            // se ejecuta síncrono si no toma mucho o se delega.
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} SectionId={SectionId} CompanyKey={CompanyKey} ExecutedByName={ExecutedByName}",
                "SyncSectionTeam",
                idSeccion,
                companyKey,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new SyncSectionTeamCommand(idSeccion, companyKey, null, executedByName));
                success = result.Failure == 0;
                return Ok(result);
            }
            finally
            {
                LogManualSyncFinished("SyncSectionTeam", executedByName, startedAtUtc, success);
            }
        }

        [HttpPost("section/{idSeccion}/members")]
        public async Task<ActionResult<SyncSectionMembersResult>> SyncSectionMembers(int idSeccion, [FromQuery] string companyKey = "idat")
        {
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} SectionId={SectionId} CompanyKey={CompanyKey} ExecutedByName={ExecutedByName}",
                "SyncSectionMembers",
                idSeccion,
                companyKey,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new SyncSectionMembersCommand(idSeccion, executedByName));
                success = result.Success;
                if (!result.Success)
                    return BadRequest(result);
                return Ok(result);
            }
            finally
            {
                LogManualSyncFinished("SyncSectionMembers", executedByName, startedAtUtc, success);
            }
        }

        [HttpPost("student/{codigoAlumno}")]
        public async Task<IActionResult> SyncStudentTeams(string codigoAlumno)
        {
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} StudentCode={StudentCode} ExecutedByName={ExecutedByName}",
                "SyncStudentTeams",
                codigoAlumno,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var jobIds = await _mediator.Send(new SyncStudentTeamsCommand(codigoAlumno));
                success = true;
                return Accepted(new { Message = "Jobs encolados para el alumno", JobIds = jobIds });
            }
            finally
            {
                LogManualSyncFinished("SyncStudentTeams", executedByName, startedAtUtc, success);
            }
        }

        [HttpPost("verify/{idSeccion}")]
        public async Task<IActionResult> VerifyTeamState(int idSeccion)
        {
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} SectionId={SectionId} ExecutedByName={ExecutedByName}",
                "VerifyTeamState",
                idSeccion,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new VerifyTeamStateCommand(idSeccion, null, executedByName));
                success = result.IsValid;
                return Ok(result);
            }
            finally
            {
                LogManualSyncFinished("VerifyTeamState", executedByName, startedAtUtc, success);
            }
        }

        [HttpPost("agenda/regenerate/{idSeccion}")]
        public async Task<IActionResult> RegenerateAgenda(int idSeccion, [FromQuery] string companyKey = "idat")
        {
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} SectionId={SectionId} CompanyKey={CompanyKey} ExecutedByName={ExecutedByName}",
                "RegenerateAgenda",
                idSeccion,
                companyKey,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new RegenerateAgendaCommand(idSeccion, companyKey, null, executedByName));
                success = result.IsValid;
                return Ok(result);
            }
            finally
            {
                LogManualSyncFinished("RegenerateAgenda", executedByName, startedAtUtc, success);
            }
        }

        /// <summary>
        /// Nuclear Option: Bulk regenerates agendas for all active sections in the pilot.
        /// Restricted exclusively to IT superadministrators.
        /// </summary>
        [HttpPost("agenda/regenerate-pilot")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "IT")]
        public async Task<IActionResult> RegeneratePilotAgendas([FromQuery] string companyKey = "idat")
        {
            var executedByName = GetManualExecutorName();
            _logger.LogWarning(
                "NuclearSyncRequest Action={Action} CompanyKey={CompanyKey} ExecutedByName={ExecutedByName}",
                "RegeneratePilotAgendas",
                companyKey,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new RegeneratePilotAgendasCommand(companyKey, executedByName));
                success = result.IsValid;
                return Ok(result);
            }
            finally
            {
                LogManualSyncFinished("RegeneratePilotAgendas", executedByName, startedAtUtc, success);
            }
        }

        /// <summary>
        /// Recreates a team: deletes from Graph, soft-deletes local records,
        /// provisions a brand new team, and syncs students.
        /// </summary>
        [HttpPost("recreate/{idSeccion}")]
        public async Task<IActionResult> RecreateTeam(int idSeccion, [FromQuery] string companyKey = "idat")
        {
            var executedByName = GetManualExecutorName();
            _logger.LogInformation(
                "ManualSyncRequest Action={Action} SectionId={SectionId} CompanyKey={CompanyKey} ExecutedByName={ExecutedByName}",
                "RecreateTeam",
                idSeccion,
                companyKey,
                executedByName);

            var startedAtUtc = DateTime.UtcNow;
            var success = false;
            try
            {
                var result = await _mediator.Send(new RecreateTeamCommand(idSeccion, companyKey, executedByName));
                success = result.Success;
                if (result.Success)
                    return Ok(result);
                return BadRequest(result);
            }
            finally
            {
                LogManualSyncFinished("RecreateTeam", executedByName, startedAtUtc, success);
            }
        }

        [HttpGet("automatic-status")]
        public async Task<IActionResult> GetAutomaticSyncStatus()
        {
            try
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                var normalizedCompanyKey = (tenant.CompanyKey ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(normalizedCompanyKey))
                {
                    return Ok(new { IsAutomaticSyncRunning = false });
                }

                await CleanupStaleAutomaticSyncExecutionsAsync(normalizedCompanyKey, HttpContext.RequestAborted);

                var isRunning = await GetAutomaticSyncStatusWithEfAsync(normalizedCompanyKey, HttpContext.RequestAborted);
                return Ok(new { IsAutomaticSyncRunning = isRunning });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al consultar el estado de sincronización automática.");
                return Ok(new { IsAutomaticSyncRunning = false });
            }
        }

        [HttpPost("unlock-automatic-sync")]
        public async Task<IActionResult> UnlockAutomaticSync()
        {
            try
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                var normalizedCompanyKey = (tenant.CompanyKey ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(normalizedCompanyKey))
                {
                    return Ok(new { Success = true, Message = "Sin tenant asignado." });
                }

                var companyConfigId = await _centralDbContext.CompanyConfigs
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

                if (companyConfigId != 0)
                {
                    var uncompletedExecutions = await _centralDbContext.SyncScheduleExecutions
                        .Where(e => e.CompanyConfigId == companyConfigId
                                    && e.CompletedAtUtc == null)
                        .ToListAsync(HttpContext.RequestAborted);

                    if (uncompletedExecutions.Any())
                    {
                        var nowUtc = DateTime.UtcNow;
                        foreach (var exec in uncompletedExecutions)
                        {
                            exec.CompletedAtUtc = nowUtc;
                            exec.Status = "Unlocked";
                            exec.ErrorMessage = "Desbloqueado manualmente por usuario.";
                        }
                        await _centralDbContext.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }

                return Ok(new { Success = true, Message = "Sincronización automática desbloqueada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desbloquear la sincronización automática.");
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        private async Task CleanupStaleAutomaticSyncExecutionsAsync(string normalizedCompanyKey, CancellationToken cancellationToken)
        {
            try
            {
                var companyConfigId = await _centralDbContext.CompanyConfigs
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (companyConfigId == 0) return;

                var uncompletedExecutions = await _centralDbContext.SyncScheduleExecutions
                    .Where(e => e.CompanyConfigId == companyConfigId
                                && e.TriggerSource.ToLower() == "schedulerservice"
                                && e.CompletedAtUtc == null)
                    .ToListAsync(cancellationToken);

                if (!uncompletedExecutions.Any()) return;

                Hangfire.JobStorage? storage = null;
                try
                {
                    storage = await _tenantHangfireRuntime.GetStorageAsync(normalizedCompanyKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo obtener el storage de Hangfire para el tenant '{CompanyKey}'.", normalizedCompanyKey);
                }

                var nowUtc = DateTime.UtcNow;
                var cutoffOldUtc = nowUtc.AddMinutes(-30);
                var cutoffNoJobsUtc = nowUtc.AddMinutes(-2);
                bool updatedAny = false;

                foreach (var exec in uncompletedExecutions)
                {
                    if (exec.TriggeredAtUtc < cutoffOldUtc)
                    {
                        exec.CompletedAtUtc = nowUtc;
                        exec.Status = "TimedOut";
                        exec.ErrorMessage = "Ejecución marcada como expirada por inactividad (>30 min).";
                        updatedAny = true;
                        continue;
                    }

                    if (storage != null && !string.IsNullOrWhiteSpace(exec.JobIds))
                    {
                        var monitoring = storage.GetMonitoringApi();
                        var jobIds = exec.JobIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        bool isAnyJobPending = false;

                        foreach (var jobId in jobIds)
                        {
                            var details = monitoring.JobDetails(jobId);
                            if (details != null)
                            {
                                var lastState = details.History?.OrderByDescending(h => h.CreatedAt).FirstOrDefault()?.StateName;
                                if (IsPendingState(lastState))
                                {
                                    isAnyJobPending = true;
                                    break;
                                }
                            }
                        }

                        if (!isAnyJobPending)
                        {
                            exec.CompletedAtUtc = nowUtc;
                            if (string.Equals(exec.Status, "Started", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(exec.Status, "Processing", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(exec.Status, "Running", StringComparison.OrdinalIgnoreCase))
                            {
                                exec.Status = "Succeeded";
                            }
                            updatedAny = true;
                        }
                    }
                    else if (exec.TriggeredAtUtc < cutoffNoJobsUtc)
                    {
                        exec.CompletedAtUtc = nowUtc;
                        exec.Status = "Completed";
                        updatedAny = true;
                    }
                }

                if (updatedAny)
                {
                    await _centralDbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al limpiar ejecuciones expiradas de sincronización automática.");
            }
        }

        private static bool IsPendingState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state)) return false;
            return string.Equals(state, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Processing", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Awaiting", StringComparison.OrdinalIgnoreCase);
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

        private void LogManualSyncFinished(string action, string executedByName, DateTime startedAtUtc, bool success)
        {
            var finishedAtUtc = DateTime.UtcNow;
            var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;

            _logger.LogInformation(
                "ManualSyncFinished Action={Action} ExecutedByName={ExecutedByName} Success={Success} StartedAtUtc={StartedAtUtc} FinishedAtUtc={FinishedAtUtc} DurationMs={DurationMs}",
                action,
                executedByName,
                success,
                startedAtUtc,
                finishedAtUtc,
                durationMs);
        }

        private async Task<bool?> TryGetAutomaticSyncStatusFromStoredProcedureAsync(string normalizedCompanyKey, CancellationToken cancellationToken)
        {
            if (_centralDbContext is not DbContext centralDb)
            {
                return null;
            }

            var provider = centralDb.Database.ProviderName ?? string.Empty;
            if (!provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var connection = centralDb.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.CommandText = "dbo.cTeamsAutomaticSyncStatus";

            var param = command.CreateParameter();
            param.ParameterName = "@CompanyKey";
            param.Value = normalizedCompanyKey;
            command.Parameters.Add(param);

            try
            {
                var scalar = await command.ExecuteScalarAsync(cancellationToken);
                if (scalar is null || scalar == DBNull.Value)
                {
                    return false;
                }

                return scalar switch
                {
                    bool flag => flag,
                    byte b => b != 0,
                    short s => s != 0,
                    int i => i != 0,
                    long l => l != 0,
                    string text => text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase),
                    _ => Convert.ToBoolean(scalar)
                };
            }
            catch (SqlException ex) when (ex.Number == 2812)
            {
                return null;
            }
        }

        private async Task<bool> GetAutomaticSyncStatusWithEfAsync(string normalizedCompanyKey, CancellationToken cancellationToken)
        {
            var companyConfigId = await _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (companyConfigId == 0)
            {
                return false;
            }

            var cutoffUtc = DateTime.UtcNow.AddMinutes(-30);

            return await _centralDbContext.SyncScheduleExecutions
                .AsNoTracking()
                .AnyAsync(
                    e => e.CompanyConfigId == companyConfigId
                         && e.TriggerSource.ToLower() == "schedulerservice"
                         && e.CompletedAtUtc == null
                         && e.TriggeredAtUtc >= cutoffUtc
                         && (
                             e.Status.ToLower() == "started"
                             || e.Status.ToLower() == "processing"
                             || e.Status.ToLower() == "running"
                         ),
                    cancellationToken);
        }
    }
}

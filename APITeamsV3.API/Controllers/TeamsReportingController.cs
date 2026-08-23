using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Stats.Queries;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION,ALL")]
    public class TeamsReportingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly ICentralDbContext _centralDbContext;
        private readonly ISmartDbContext _smartDbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<TeamsReportingController> _logger;

        public TeamsReportingController(
            IMediator mediator,
            ITenantProvider tenantProvider,
            ICentralDbContext centralDbContext,
            ISmartDbContext smartDbContext,
            IEncryptionService encryptionService,
            ILogger<TeamsReportingController> logger)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDbContext = centralDbContext;
            _smartDbContext = smartDbContext;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        [HttpGet("teams-by-section/{idSeccion}")]
        public async Task<ActionResult<List<TeamBySectionDto>>> GetTeamsBySection(int idSeccion)
        {
            var result = await _mediator.Send(new GetTeamsBySectionQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("section-details/{idSeccion}")]
        public async Task<ActionResult<List<SectionDetailsDto>>> GetSectionDetails(int idSeccion)
        {
            var result = await _mediator.Send(new GetSectionDetailsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("student-sync-status/{idSeccion}")]
        public async Task<ActionResult<List<StudentSyncStatusDto>>> GetStudentSyncStatus(int idSeccion)
        {
            var result = await _mediator.Send(new GetStudentSyncStatusQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("tenancy-stats")]
        public async Task<ActionResult<List<TenancyStatsDto>>> GetTenancyStats()
        {
            var result = await _mediator.Send(new GetTenancyStatsQuery());
            return Ok(result);
        }

        [HttpGet("dashboard-summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            var rows = await _mediator.Send(new GetTenancyStatsQuery());
            var pilotSchedule = await _mediator.Send(new GetPilotScheduleQuery());

            var normalizedCompanyKey = tenant.CompanyKey.Trim().ToLowerInvariant();
            var companyConfig = await _centralDbContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey,
                    HttpContext.RequestAborted);

            var result = new DashboardSummaryDto
            {
                CompanyKey = tenant.CompanyKey,
                DisplayName = companyConfig?.DisplayName ?? tenant.CompanyKey.ToUpperInvariant(),
                IsPilotMode = companyConfig?.IsPilotMode ?? false,
                PilotSectionsConfigured = companyConfig?.PilotSections.Count ?? 0,
                DefaultChannelName = companyConfig?.DefaultChannelName ?? string.Empty,
                MeetingPolicyMode = companyConfig?.MeetingPolicyMode ?? string.Empty,
                TimeZoneId = companyConfig?.TimeZoneId ?? tenant.TimeZoneId ?? string.Empty,
                Rows = rows,
                PilotSchedule = pilotSchedule
            };

            return Ok(result);
        }

        [HttpGet("pending-sections-by-period/{codigoPeriodo}")]
        public async Task<ActionResult<List<PendingSectionDto>>> GetPendingSectionsByPeriod(
            string codigoPeriodo,
            [FromQuery] string? fechaInicioClases = null)
        {
            var result = await _mediator.Send(new GetPendingSectionsByPeriodQuery(codigoPeriodo, fechaInicioClases));
            return Ok(result);
        }

        [HttpGet("logs")]
        public async Task<ActionResult<List<TeamsLogOperativoDto>>> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? tipo = null,
            [FromQuery] string? severidad = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? referencia = null,
            [FromQuery] string? jobId = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] string? scope = null,
            [FromQuery] string? companyKey = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            var isIt = User.IsInRole("IT");
            var fetchAllCompanies = isIt && !string.Equals(scope, "current", StringComparison.OrdinalIgnoreCase);

            var filter = new LogsFilter(
                Tipo: tipo,
                Severidad: severidad,
                Entidad: entidad,
                Referencia: referencia,
                JobId: jobId,
                Search: search,
                FechaDesde: fechaDesde);

            if (fetchAllCompanies)
            {
                var result = await GetLogsAcrossCompaniesAsync(
                    page,
                    pageSize,
                    filter,
                    companyKey,
                    HttpContext.RequestAborted);

                return Ok(result);
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            var currentTenantLogs = await _mediator.Send(new GetLogsQuery
            {
                Page = page,
                PageSize = pageSize,
                TipoFiltro = filter.Tipo,
                SeveridadFiltro = filter.Severidad,
                EntidadFiltro = filter.Entidad,
                ReferenciaFiltro = filter.Referencia,
                JobIdFiltro = filter.JobId,
                SearchTerm = filter.Search,
                FechaDesde = filter.FechaDesde
            });

            foreach (var log in currentTenantLogs)
            {
                log.CompanyKey = tenant.CompanyKey;
            }

            return Ok(currentTenantLogs);
        }

        [HttpGet("logs-summary")]
        public async Task<ActionResult<LogOperationalSummaryDto>> GetLogsSummary(
            [FromQuery] string? tipo = null,
            [FromQuery] string? severidad = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? referencia = null,
            [FromQuery] string? jobId = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] string? scope = null,
            [FromQuery] string? companyKey = null)
        {
            var isIt = User.IsInRole("IT");
            var fetchAllCompanies = isIt && !string.Equals(scope, "current", StringComparison.OrdinalIgnoreCase);

            var filter = new LogsFilter(
                Tipo: tipo,
                Severidad: severidad,
                Entidad: entidad,
                Referencia: referencia,
                JobId: jobId,
                Search: search,
                FechaDesde: fechaDesde);

            if (fetchAllCompanies)
            {
                var result = await GetSummaryAcrossCompaniesAsync(
                    filter,
                    companyKey,
                    HttpContext.RequestAborted);

                return Ok(result);
            }

            var currentTenantSummary = await _mediator.Send(new GetLogSummaryQuery
            {
                TipoFiltro = filter.Tipo,
                SeveridadFiltro = filter.Severidad,
                EntidadFiltro = filter.Entidad,
                ReferenciaFiltro = filter.Referencia,
                JobIdFiltro = filter.JobId,
                SearchTerm = filter.Search,
                FechaDesde = filter.FechaDesde
            });

            return Ok(currentTenantSummary);
        }

        [HttpDelete("logs")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
        public async Task<IActionResult> ClearLogs(
            [FromQuery] string companyKey = "all",
            [FromQuery] string? tipo = null,
            [FromQuery] string? severidad = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? scope = null)
        {
            var effectiveScope = (scope ?? string.Empty).Trim().ToLowerInvariant();
            var targetCompanyKey = companyKey;

            if (!User.IsInRole("IT") || effectiveScope == "current")
            {
                targetCompanyKey = _tenantProvider.GetCurrentTenant().CompanyKey;
            }

            var companiesQuery = _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.IsActive && c.SmartConnectionString != null && c.SmartConnectionString != string.Empty);

            if (!string.IsNullOrWhiteSpace(targetCompanyKey) && targetCompanyKey.ToLower() != "all")
            {
                var normalizedCompanyKey = targetCompanyKey.Trim().ToLowerInvariant();
                companiesQuery = companiesQuery.Where(c => c.CompanyKey.ToLower() == normalizedCompanyKey);
            }

            var companies = await companiesQuery
                .OrderBy(c => c.CompanyKey)
                .Select(c => new { c.CompanyKey, c.SmartConnectionString })
                .ToListAsync(HttpContext.RequestAborted);

            int totalDeleted = 0;

            foreach (var company in companies)
            {
                string decryptedConnection;
                try
                {
                    decryptedConnection = _encryptionService.Decrypt(company.SmartConnectionString);
                }
                catch
                {
                    decryptedConnection = company.SmartConnectionString;
                }

                if (string.IsNullOrWhiteSpace(decryptedConnection)) continue;

                try
                {
                    await using var connection = new SqlConnection(decryptedConnection);
                    await connection.OpenAsync(HttpContext.RequestAborted);

                    var sql = new StringBuilder();
                    sql.AppendLine("DELETE FROM [TeamsLogOperativo] WHERE 1 = 1");

                    await using var command = connection.CreateCommand();
                    command.CommandType = CommandType.Text;

                    if (!string.IsNullOrWhiteSpace(tipo))
                    {
                        sql.AppendLine("AND [Tipo] = @Tipo");
                        command.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.NVarChar, 50) { Value = tipo.Trim() });
                    }
                    if (!string.IsNullOrWhiteSpace(severidad))
                    {
                        sql.AppendLine("AND [Severidad] = @Severidad");
                        command.Parameters.Add(new SqlParameter("@Severidad", SqlDbType.NVarChar, 50) { Value = severidad.Trim() });
                    }
                    if (!string.IsNullOrWhiteSpace(entidad))
                    {
                        sql.AppendLine("AND [EntidadAfectada] = @Entidad");
                        command.Parameters.Add(new SqlParameter("@Entidad", SqlDbType.NVarChar, 200) { Value = entidad.Trim() });
                    }

                    command.CommandText = sql.ToString();
                    var rows = await command.ExecuteNonQueryAsync(HttpContext.RequestAborted);
                    totalDeleted += rows;
                    _logger.LogInformation("Deleted {Rows} operational log entries from {CompanyKey}.", rows, company.CompanyKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete operational logs for company {CompanyKey}", company.CompanyKey);
                }
            }

            return Ok(new { message = $"Se eliminaron {totalDeleted} registros de logs correctamente.", deletedCount = totalDeleted });
        }

        [HttpGet("report-schedules")]
        public async Task<ActionResult<List<ScheduleReportDto>>> GetScheduleReport()
        {
            var result = await _mediator.Send(new GetScheduleReportQuery());
            return Ok(result);
        }

        [HttpGet("report-team-members")]
        public async Task<ActionResult<List<TeamMemberReportDto>>> GetTeamMembersReport([FromQuery] string? idTeamsGroup)
        {
            var result = await _mediator.Send(new GetTeamMembersReportQuery { IdTeamsGroup = idTeamsGroup });
            return Ok(result);
        }

        [HttpGet("report-smart-vs-teams")]
        public async Task<ActionResult<List<SmartVsTeamsReportDto>>> GetSmartVsTeamsReport()
        {
            var result = await _mediator.Send(new GetSmartVsTeamsReportQuery());
            return Ok(result);
        }

        [HttpGet("report-sync-progress")]
        public async Task<ActionResult<List<SyncProgressReportDto>>> GetSyncProgressReport()
        {
            var result = await _mediator.Send(new GetSyncProgressReportQuery());
            return Ok(result);
        }

        private async Task<List<TeamsLogOperativoDto>> GetLogsAcrossCompaniesAsync(
            int page,
            int pageSize,
            LogsFilter filter,
            string? companyKeyFilter,
            CancellationToken cancellationToken)
        {
            var companiesQuery = _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.IsActive && c.SmartConnectionString != null && c.SmartConnectionString != string.Empty);

            if (!string.IsNullOrWhiteSpace(companyKeyFilter))
            {
                var normalizedCompanyKey = companyKeyFilter.Trim().ToLowerInvariant();
                companiesQuery = companiesQuery.Where(c => c.CompanyKey.ToLower() == normalizedCompanyKey);
            }

            var companies = await companiesQuery
                .OrderBy(c => c.CompanyKey)
                .Select(c => new { c.CompanyKey, c.SmartConnectionString })
                .ToListAsync(cancellationToken);

            if (companies.Count == 0)
            {
                return [];
            }

            var takePerCompany = Math.Clamp(page * pageSize, pageSize, 10000);
            var allLogs = new List<TeamsLogOperativoDto>(takePerCompany * companies.Count);

            foreach (var company in companies)
            {
                string decryptedConnection;
                try
                {
                    decryptedConnection = _encryptionService.Decrypt(company.SmartConnectionString);
                }
                catch
                {
                    decryptedConnection = company.SmartConnectionString;
                }

                if (string.IsNullOrWhiteSpace(decryptedConnection))
                {
                    continue;
                }

                try
                {
                    await using var connection = new SqlConnection(decryptedConnection);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = BuildLogsCommand(connection, takePerCompany, filter);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        allLogs.Add(new TeamsLogOperativoDto
                        {
                            Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            Tipo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            EntidadAfectada = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Referencia = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            Mensaje = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            ContextoTecnico = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            Severidad = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            JobId = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            Fecha = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                            CompanyKey = company.CompanyKey
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron consultar logs para la empresa {CompanyKey}.", company.CompanyKey);
                }
            }

            var skip = (page - 1) * pageSize;
            return allLogs
                .OrderByDescending(l => l.Fecha)
                .ThenByDescending(l => l.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToList();
        }

        private static SqlCommand BuildLogsCommand(SqlConnection connection, int top, LogsFilter filter)
        {
            var sql = new StringBuilder();
            sql.AppendLine("SELECT TOP (@Top) [Id], [Tipo], [EntidadAfectada], [Referencia], [Mensaje], [ContextoTecnico], [Severidad], ISNULL([JobId], ''), [Fecha]");
            sql.AppendLine("FROM [TeamsLogOperativo] WITH (NOLOCK)");
            sql.AppendLine("WHERE 1 = 1");

            var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = top });

            if (!string.IsNullOrWhiteSpace(filter.Tipo))
            {
                sql.AppendLine("AND [Tipo] = @Tipo");
                command.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.NVarChar, 50) { Value = filter.Tipo.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Severidad))
            {
                sql.AppendLine("AND [Severidad] = @Severidad");
                command.Parameters.Add(new SqlParameter("@Severidad", SqlDbType.NVarChar, 50) { Value = filter.Severidad.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Entidad))
            {
                sql.AppendLine("AND [EntidadAfectada] = @Entidad");
                command.Parameters.Add(new SqlParameter("@Entidad", SqlDbType.NVarChar, 200) { Value = filter.Entidad.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Referencia))
            {
                sql.AppendLine("AND [Referencia] LIKE @Referencia");
                command.Parameters.Add(new SqlParameter("@Referencia", SqlDbType.NVarChar, 400) { Value = $"%{filter.Referencia.Trim()}%" });
            }

            if (!string.IsNullOrWhiteSpace(filter.JobId))
            {
                sql.AppendLine("AND ISNULL([JobId], '') LIKE @JobId");
                command.Parameters.Add(new SqlParameter("@JobId", SqlDbType.NVarChar, 400) { Value = $"%{filter.JobId.Trim()}%" });
            }

            if (filter.FechaDesde.HasValue)
            {
                sql.AppendLine("AND [Fecha] >= @FechaDesde");
                command.Parameters.Add(new SqlParameter("@FechaDesde", SqlDbType.DateTime2) { Value = filter.FechaDesde.Value });
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                sql.AppendLine("AND (");
                sql.AppendLine("    [Tipo] LIKE @Search");
                sql.AppendLine("    OR [EntidadAfectada] LIKE @Search");
                sql.AppendLine("    OR [Referencia] LIKE @Search");
                sql.AppendLine("    OR [Mensaje] LIKE @Search");
                sql.AppendLine("    OR [Severidad] LIKE @Search");
                sql.AppendLine("    OR ISNULL([JobId], '') LIKE @Search");
                sql.AppendLine("    OR ISNULL([ContextoTecnico], '') LIKE @Search");
                sql.AppendLine(")");

                command.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 4000) { Value = $"%{filter.Search.Trim()}%" });
            }

            sql.AppendLine("ORDER BY [Fecha] DESC, [Id] DESC;");
            command.CommandText = sql.ToString();
            return command;
        }

        private async Task<LogOperationalSummaryDto> GetSummaryAcrossCompaniesAsync(
            LogsFilter filter,
            string? companyKeyFilter,
            CancellationToken cancellationToken)
        {
            var companiesQuery = _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.IsActive && c.SmartConnectionString != null && c.SmartConnectionString != string.Empty);

            if (!string.IsNullOrWhiteSpace(companyKeyFilter))
            {
                var normalizedCompanyKey = companyKeyFilter.Trim().ToLowerInvariant();
                companiesQuery = companiesQuery.Where(c => c.CompanyKey.ToLower() == normalizedCompanyKey);
            }

            var companies = await companiesQuery
                .Select(c => new { c.CompanyKey, c.SmartConnectionString })
                .ToListAsync(cancellationToken);

            var result = new LogOperationalSummaryDto();

            foreach (var company in companies)
            {
                string decryptedConnection;
                try
                {
                    decryptedConnection = _encryptionService.Decrypt(company.SmartConnectionString);
                }
                catch
                {
                    decryptedConnection = company.SmartConnectionString;
                }

                if (string.IsNullOrWhiteSpace(decryptedConnection)) continue;

                try
                {
                    await using var connection = new SqlConnection(decryptedConnection);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = BuildSummaryCommand(connection, filter);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var entidad = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        var tipo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var count = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                        if (tipo == "Success")
                        {
                            if (entidad == "Student" || entidad == "StudentSync" || entidad == "Members")
                                result.StudentsSuccess += count;
                            else if (entidad == "Agenda")
                                result.AgendasSuccess += count;
                            else if (entidad == "Team")
                                result.TeamsSuccess += count;
                        }

                        if (tipo == "Error") result.TotalErrors += count;
                        if (tipo == "Warning") result.TotalWarnings += count;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo consultar el resumen para la empresa {CompanyKey}.", company.CompanyKey);
                }
            }

            return result;
        }

        private static SqlCommand BuildSummaryCommand(SqlConnection connection, LogsFilter filter)
        {
            var sql = new StringBuilder();
            sql.AppendLine("SELECT [EntidadAfectada], [Tipo], COUNT(*)");
            sql.AppendLine("FROM [TeamsLogOperativo] WITH (NOLOCK)");
            sql.AppendLine("WHERE 1 = 1");

            var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;

            if (!string.IsNullOrWhiteSpace(filter.Tipo))
            {
                sql.AppendLine("AND [Tipo] = @Tipo");
                command.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.NVarChar, 50) { Value = filter.Tipo.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Severidad))
            {
                sql.AppendLine("AND [Severidad] = @Severidad");
                command.Parameters.Add(new SqlParameter("@Severidad", SqlDbType.NVarChar, 50) { Value = filter.Severidad.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Entidad))
            {
                sql.AppendLine("AND [EntidadAfectada] = @Entidad");
                command.Parameters.Add(new SqlParameter("@Entidad", SqlDbType.NVarChar, 200) { Value = filter.Entidad.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(filter.Referencia))
            {
                sql.AppendLine("AND [Referencia] LIKE @Referencia");
                command.Parameters.Add(new SqlParameter("@Referencia", SqlDbType.NVarChar, 400) { Value = $"%{filter.Referencia.Trim()}%" });
            }

            if (!string.IsNullOrWhiteSpace(filter.JobId))
            {
                sql.AppendLine("AND ISNULL([JobId], '') LIKE @JobId");
                command.Parameters.Add(new SqlParameter("@JobId", SqlDbType.NVarChar, 400) { Value = $"%{filter.JobId.Trim()}%" });
            }

            if (filter.FechaDesde.HasValue)
            {
                sql.AppendLine("AND [Fecha] >= @FechaDesde");
                command.Parameters.Add(new SqlParameter("@FechaDesde", SqlDbType.DateTime2) { Value = filter.FechaDesde.Value });
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                sql.AppendLine("AND (");
                sql.AppendLine("    [Tipo] LIKE @Search");
                sql.AppendLine("    OR [EntidadAfectada] LIKE @Search");
                sql.AppendLine("    OR [Referencia] LIKE @Search");
                sql.AppendLine("    OR [Mensaje] LIKE @Search");
                sql.AppendLine("    OR [Severidad] LIKE @Search");
                sql.AppendLine("    OR ISNULL([JobId], '') LIKE @Search");
                sql.AppendLine("    OR ISNULL([ContextoTecnico], '') LIKE @Search");
                sql.AppendLine(")");

                command.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 4000) { Value = $"%{filter.Search.Trim()}%" });
            }

            sql.AppendLine("GROUP BY [EntidadAfectada], [Tipo]");
            command.CommandText = sql.ToString();
            return command;
        }

        [HttpGet("detailed-report")]
        public async Task<ActionResult<APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport.DetailedReportResultDto>> GetDetailedReport([FromQuery] APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport.GetDetailedReportQuery query)
        {
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("detailed-report/export")]
        public async Task<ActionResult> StartDetailedReportExport([FromBody] APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport.GetDetailedReportQuery query, [FromServices] APITeamsV3.Infrastructure.Services.TenantHangfireRuntime tenantHangfireRuntime)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            var jobId = Guid.NewGuid().ToString("N");
            var executedBy = User.Identity?.Name ?? "Sistema";
            
            var storage = await tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey);
            var backgroundJobClient = new Hangfire.BackgroundJobClient(storage);
            
            backgroundJobClient.Enqueue<APITeamsV3.Application.Common.Interfaces.IReportExportJob>(x => x.ExecuteExportAsync(query, tenant.CompanyKey, jobId, executedBy));
            return Ok(new { JobId = jobId });
        }

        [HttpGet("detailed-report/export/{jobId}")]
        public ActionResult GetDetailedReportExportStatus(string jobId)
        {
            var folderPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "wwwroot", "exports");
            var readyFile = System.IO.Path.Combine(folderPath, $"{jobId}.ready");
            var errorFile = System.IO.Path.Combine(folderPath, $"{jobId}.error");
            
            if (System.IO.File.Exists(errorFile))
            {
                var error = System.IO.File.ReadAllText(errorFile);
                return BadRequest(new { Status = "Error", Message = error });
            }

            if (System.IO.File.Exists(readyFile))
            {
                var url = $"/exports/{jobId}.xlsx";
                return Ok(new { Status = "Ready", Url = url });
            }

            return Ok(new { Status = "Processing" });
        }

        [HttpGet("team-sessions-report")]
        public async Task<ActionResult<TeamSessionsReportResponseDto>> GetTeamSessionsReport(
            [FromQuery] bool? tieneSesiones = null,
            [FromQuery] string? periodo = null,
            [FromQuery] string? search = null,
            [FromQuery] string? docente = null,
            [FromQuery] bool modoExcluirDocente = false,
            [FromQuery] bool? soloConAlumnos = null,
            [FromQuery] bool? soloPiloto = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 100000);
            pageNumber = Math.Max(1, pageNumber);

            try
            {
                var connection = _smartDbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(HttpContext.RequestAborted);
                }

                var tenant = _tenantProvider.GetCurrentTenant();
                var whereClauses = new List<string> { "TE.EstadoTeam = 'A'" };
                var oneYearAgo = DateTime.UtcNow.AddYears(-1);
                bool usesOneYearAgo = false;

                if (!string.IsNullOrWhiteSpace(tenant.CompanyKey))
                {
                    var normalizedKey = tenant.CompanyKey.Trim().ToLowerInvariant();
                    var companyConfig = await _centralDbContext.CompanyConfigs
                        .Include(c => c.PilotSections)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.IsActive && c.CompanyKey.ToLower() == normalizedKey, HttpContext.RequestAborted);

                    bool applyPilotFilter = soloPiloto ?? (companyConfig?.IsPilotMode ?? false);

                    if (applyPilotFilter && companyConfig != null && companyConfig.PilotSections != null && companyConfig.PilotSections.Any())
                    {
                        var pilotIds = companyConfig.PilotSections.Select(p => p.IdSeccion).ToList();
                        whereClauses.Add($"TE.IdSeccionSmart IN ({string.Join(",", pilotIds)})");
                    }
                    else if (string.IsNullOrWhiteSpace(periodo))
                    {
                        whereClauses.Add("TE.FechaCreacion >= @OneYearAgo");
                        usesOneYearAgo = true;
                    }
                }
                else if (string.IsNullOrWhiteSpace(periodo))
                {
                    whereClauses.Add("TE.FechaCreacion >= @OneYearAgo");
                    usesOneYearAgo = true;
                }

                if (!string.IsNullOrWhiteSpace(periodo))
                {
                    whereClauses.Add("PE.Codigo = @Periodo");
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClauses.Add("(COALESCE(SE.Codigo, CAST(TE.IdSeccionSmart AS NVARCHAR(50))) LIKE @Search OR TE.NombreTeam LIKE @Search OR TE.IdTeamsGroup LIKE @Search)");
                }

                if (!string.IsNullOrWhiteSpace(docente))
                {
                    if (modoExcluirDocente)
                    {
                        whereClauses.Add("(COALESCE(TH.CorreoFacilitador, TPG.EmailFacilitador, FC.EmailInstitucion, '') NOT LIKE @Docente)");
                    }
                    else
                    {
                        whereClauses.Add("(COALESCE(TH.CorreoFacilitador, TPG.EmailFacilitador, FC.EmailInstitucion, '') LIKE @Docente)");
                    }
                }

                string whereSql = string.Join(" AND ", whereClauses);

            string havingFilter = tieneSesiones switch
            {
                true => "AND (COALESCE(TH.TotalSesiones, 0) > 0 OR NULLIF(TRIM(SH.UrlClaseVirtual), '') IS NOT NULL)",
                false => "AND COALESCE(TH.TotalSesiones, 0) = 0 AND NULLIF(TRIM(SH.UrlClaseVirtual), '') IS NULL",
                _ => string.Empty
            };

            if (soloConAlumnos.HasValue)
            {
                if (soloConAlumnos.Value)
                {
                    havingFilter += " AND COALESCE(AC.CantidadAlumnos, 0) > 0";
                }
                else
                {
                    havingFilter += " AND COALESCE(AC.CantidadAlumnos, 0) = 0";
                }
            }

            string summarySql = $@"
                WITH ActiveSections AS (
                    SELECT DISTINCT IdSeccionSmart 
                    FROM TeamsEquipos WITH(NOLOCK) 
                    WHERE EstadoTeam = 'A'
                ),
                TH_Agg AS (
                    SELECT 
                        IdCurso,
                        COUNT(DISTINCT NumeroReunion) AS TotalSesiones,
                        MAX(NULLIF(TRIM(CorreoFacilitador), '')) AS CorreoFacilitador
                    FROM TeamsHorarios WITH(NOLOCK)
                    WHERE Estado = 'A'
                      AND IdCurso IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdCurso
                ),
                SH_Agg AS (
                    SELECT 
                        IdSeccion,
                        MAX(NULLIF(TRIM(UrlClaseVirtual), '')) AS UrlClaseVirtual,
                        MAX(NULLIF(TRIM(IdEvento), '')) AS IdEvento,
                        COUNT(*) AS TotalHorariosSmart
                    FROM SeccionHorario WITH(NOLOCK)
                    WHERE IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                SP_Agg AS (
                    SELECT 
                        IdSeccion,
                        MAX(IdActor) AS IdActor
                    FROM SeccionProfesor WITH(NOLOCK)
                    WHERE EsResponsable = 1
                      AND IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                AC_Agg AS (
                    SELECT 
                        IdSeccion,
                        COUNT(DISTINCT IdAlumno) AS CantidadAlumnos
                    FROM AlumnoCurso WITH(NOLOCK)
                    WHERE EsMatricula = 1 AND Estado <> 'X'
                      AND IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                TeamCounts AS (
                    SELECT 
                        TE.IdTeamsGroup,
                        CASE WHEN COALESCE(TH.TotalSesiones, 0) > 0 OR NULLIF(TRIM(SH.UrlClaseVirtual), '') IS NOT NULL THEN 1 ELSE 0 END AS HasSessions
                    FROM TeamsEquipos TE WITH(NOLOCK)
                    LEFT JOIN Seccion SE WITH(NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion
                    LEFT JOIN SH_Agg SH ON SE.IdSeccion = SH.IdSeccion
                    LEFT JOIN Promocion PR WITH(NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    LEFT JOIN Periodo PE WITH(NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                    LEFT JOIN TeamsProgramacionGeneral TPG WITH(NOLOCK) ON TE.IdSeccionSmart = TPG.IdCurso
                    LEFT JOIN SP_Agg SP ON SE.IdSeccion = SP.IdSeccion
                    LEFT JOIN Facilitador FC WITH(NOLOCK) ON SP.IdActor = FC.IdFacilitador
                    LEFT JOIN TH_Agg TH ON TE.IdSeccionSmart = TH.IdCurso
                    LEFT JOIN AC_Agg AC ON SE.IdSeccion = AC.IdSeccion
                    WHERE {whereSql} {havingFilter}
                )
                SELECT 
                    COUNT(*) AS TotalEquipos,
                    SUM(HasSessions) AS EquiposConSesiones,
                    SUM(CASE WHEN HasSessions = 0 THEN 1 ELSE 0 END) AS EquiposSinSesiones
                FROM TeamCounts;";

            await using var summaryCmd = connection.CreateCommand();
            summaryCmd.CommandText = summarySql;
            summaryCmd.CommandTimeout = 180;
            if (usesOneYearAgo)
            {
                var pDate = summaryCmd.CreateParameter();
                pDate.ParameterName = "@OneYearAgo";
                pDate.Value = oneYearAgo;
                summaryCmd.Parameters.Add(pDate);
            }
            if (!string.IsNullOrWhiteSpace(periodo))
            {
                var p1 = summaryCmd.CreateParameter();
                p1.ParameterName = "@Periodo";
                p1.Value = periodo.Trim();
                summaryCmd.Parameters.Add(p1);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var p2 = summaryCmd.CreateParameter();
                p2.ParameterName = "@Search";
                p2.Value = $"%{search.Trim()}%";
                summaryCmd.Parameters.Add(p2);
            }
            if (!string.IsNullOrWhiteSpace(docente))
            {
                var pDocente = summaryCmd.CreateParameter();
                pDocente.ParameterName = "@Docente";
                pDocente.Value = $"%{docente.Trim()}%";
                summaryCmd.Parameters.Add(pDocente);
            }

            int totalEquipos = 0, conSesiones = 0, sinSesiones = 0;
            await using (var reader = await summaryCmd.ExecuteReaderAsync(HttpContext.RequestAborted))
            {
                if (await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    totalEquipos = reader["TotalEquipos"] != DBNull.Value ? Convert.ToInt32(reader["TotalEquipos"]) : 0;
                    conSesiones = reader["EquiposConSesiones"] != DBNull.Value ? Convert.ToInt32(reader["EquiposConSesiones"]) : 0;
                    sinSesiones = reader["EquiposSinSesiones"] != DBNull.Value ? Convert.ToInt32(reader["EquiposSinSesiones"]) : 0;
                }
            }

            string dataSql = $@"
                WITH ActiveSections AS (
                    SELECT DISTINCT IdSeccionSmart 
                    FROM TeamsEquipos WITH(NOLOCK) 
                    WHERE EstadoTeam = 'A'
                ),
                TH_Agg AS (
                    SELECT 
                        IdCurso,
                        COUNT(DISTINCT NumeroReunion) AS TotalSesiones,
                        MIN(Fecha) AS PrimeraSesion,
                        MAX(Fecha) AS UltimaSesion,
                        MAX(NULLIF(TRIM(CorreoFacilitador), '')) AS CorreoFacilitador,
                        MAX(NULLIF(TRIM(JoinUrl), '')) AS JoinUrl,
                        MAX(NULLIF(TRIM(IdEvento), '')) AS IdEvento
                    FROM TeamsHorarios WITH(NOLOCK)
                    WHERE Estado = 'A'
                      AND IdCurso IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdCurso
                ),
                SH_Agg AS (
                    SELECT 
                        IdSeccion,
                        MAX(NULLIF(TRIM(UrlClaseVirtual), '')) AS UrlClaseVirtual,
                        MAX(NULLIF(TRIM(IdEvento), '')) AS IdEvento,
                        COUNT(*) AS TotalHorariosSmart
                    FROM SeccionHorario WITH(NOLOCK)
                    WHERE IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                SP_Agg AS (
                    SELECT 
                        IdSeccion,
                        MAX(IdActor) AS IdActor
                    FROM SeccionProfesor WITH(NOLOCK)
                    WHERE EsResponsable = 1
                      AND IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                AC_Agg AS (
                    SELECT 
                        IdSeccion,
                        COUNT(DISTINCT IdAlumno) AS CantidadAlumnos
                    FROM AlumnoCurso WITH(NOLOCK)
                    WHERE EsMatricula = 1 AND Estado <> 'X'
                      AND IdSeccion IN (SELECT IdSeccionSmart FROM ActiveSections)
                    GROUP BY IdSeccion
                ),
                AggregatedData AS (
                    SELECT 
                        TE.IdTeamsGroup,
                        TE.IdSeccionSmart AS IdSeccion,
                        COALESCE(SE.Codigo, CAST(TE.IdSeccionSmart AS NVARCHAR(50))) AS CodigoSeccion,
                        TE.NombreTeam,
                        COALESCE(PE.Codigo, '') AS Periodo,
                        COALESCE(TH.TotalSesiones, 0) AS TotalSesiones,
                        TH.PrimeraSesion,
                        TH.UltimaSesion,
                        COALESCE(
                            TH.CorreoFacilitador,
                            NULLIF(TRIM(TPG.EmailFacilitador), ''),
                            NULLIF(TRIM(FC.EmailInstitucion), ''),
                            ''
                        ) AS CorreoFacilitador,
                        SE.FechaInicio AS FechaInicioSeccion,
                        SE.FechaFin AS FechaFinSeccion,
                        COALESCE(SH.TotalHorariosSmart, 0) AS TotalHorariosSmart,
                        COALESCE(AC.CantidadAlumnos, 0) AS CantidadAlumnos,
                        CASE WHEN TPG.IdCurso IS NOT NULL THEN 1 ELSE 0 END AS HasMetadata,
                        COALESCE(NULLIF(TRIM(SH.UrlClaseVirtual), ''), TH.JoinUrl, '') AS UrlClaseVirtual,
                        COALESCE(NULLIF(TRIM(SH.IdEvento), ''), TH.IdEvento, '') AS IdEvento
                    FROM TeamsEquipos TE WITH(NOLOCK)
                    LEFT JOIN Seccion SE WITH(NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion
                    LEFT JOIN SH_Agg SH ON SE.IdSeccion = SH.IdSeccion
                    LEFT JOIN Promocion PR WITH(NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    LEFT JOIN Periodo PE WITH(NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                    LEFT JOIN TeamsProgramacionGeneral TPG WITH(NOLOCK) ON TE.IdSeccionSmart = TPG.IdCurso
                    LEFT JOIN SP_Agg SP ON SE.IdSeccion = SP.IdSeccion
                    LEFT JOIN Facilitador FC WITH(NOLOCK) ON SP.IdActor = FC.IdFacilitador
                    LEFT JOIN TH_Agg TH ON TE.IdSeccionSmart = TH.IdCurso
                    LEFT JOIN AC_Agg AC ON SE.IdSeccion = AC.IdSeccion
                    WHERE {whereSql} {havingFilter}
                )
                SELECT *, COUNT(*) OVER() AS FilteredTotalRecords
                FROM AggregatedData
                ORDER BY TotalSesiones DESC, CodigoSeccion ASC
                {(pageSize > 0 && pageSize < 100000 ? "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;" : ";")}";

            await using var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = dataSql;
            dataCmd.CommandTimeout = 180;
            if (usesOneYearAgo)
            {
                var pDate = dataCmd.CreateParameter();
                pDate.ParameterName = "@OneYearAgo";
                pDate.Value = oneYearAgo;
                dataCmd.Parameters.Add(pDate);
            }
            if (!string.IsNullOrWhiteSpace(periodo))
            {
                var p1 = dataCmd.CreateParameter();
                p1.ParameterName = "@Periodo";
                p1.Value = periodo.Trim();
                dataCmd.Parameters.Add(p1);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var p2 = dataCmd.CreateParameter();
                p2.ParameterName = "@Search";
                p2.Value = $"%{search.Trim()}%";
                dataCmd.Parameters.Add(p2);
            }
            if (!string.IsNullOrWhiteSpace(docente))
            {
                var pDocente = dataCmd.CreateParameter();
                pDocente.ParameterName = "@Docente";
                pDocente.Value = $"%{docente.Trim()}%";
                dataCmd.Parameters.Add(pDocente);
            }

            if (pageSize > 0 && pageSize < 100000)
            {
                var pOffset = dataCmd.CreateParameter();
                pOffset.ParameterName = "@Offset";
                pOffset.Value = (pageNumber - 1) * pageSize;
                dataCmd.Parameters.Add(pOffset);

                var pPageSize = dataCmd.CreateParameter();
                pPageSize.ParameterName = "@PageSize";
                pPageSize.Value = pageSize;
                dataCmd.Parameters.Add(pPageSize);
            }

            var items = new List<TeamSessionReportItemDto>();
            int filteredTotalRecords = 0;

            await using (var reader = await dataCmd.ExecuteReaderAsync(HttpContext.RequestAborted))
            {
                while (await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    filteredTotalRecords = Convert.ToInt32(reader["FilteredTotalRecords"]);
                    int sesionesCount = Convert.ToInt32(reader["TotalSesiones"]);
                    string motivo = string.Empty;

                    if (sesionesCount == 0)
                    {
                        string correoFac = reader["CorreoFacilitador"]?.ToString()?.Trim() ?? string.Empty;
                        DateTime? fInicio = reader["FechaInicioSeccion"] is DateTime fi ? fi : (DateTime?)null;
                        DateTime? fFin = reader["FechaFinSeccion"] is DateTime ff ? ff : (DateTime?)null;
                        int totalHorarios = reader["TotalHorariosSmart"] != DBNull.Value ? Convert.ToInt32(reader["TotalHorariosSmart"]) : 0;
                        int hasMeta = reader["HasMetadata"] != DBNull.Value ? Convert.ToInt32(reader["HasMetadata"]) : 0;

                        if (string.IsNullOrWhiteSpace(correoFac))
                        {
                            motivo = "Sin Facilitador / Docente Asignado";
                        }
                        else if (totalHorarios == 0 && hasMeta == 0)
                        {
                            motivo = "Sin Horarios Programados en Smart";
                        }
                        else if (totalHorarios == 0)
                        {
                            motivo = "Sin Sesiones Programadas en Rango de Fechas";
                        }
                        else if (fInicio.HasValue && fInicio.Value.Date > DateTime.Today.AddDays(14))
                        {
                            motivo = $"Inicio Futuro ({fInicio.Value:dd/MM/yyyy})";
                        }
                        else if (fFin.HasValue && fFin.Value.Date < DateTime.Today)
                        {
                            motivo = "Curso Finalizado";
                        }
                        else
                        {
                            motivo = "Pendiente de Sincronización (Job en Cola)";
                        }
                    }

                    items.Add(new TeamSessionReportItemDto
                    {
                        IdTeamsGroup = reader["IdTeamsGroup"]?.ToString() ?? string.Empty,
                        IdSeccion = Convert.ToInt32(reader["IdSeccion"]),
                        CodigoSeccion = reader["CodigoSeccion"]?.ToString() ?? string.Empty,
                        NombreTeam = reader["NombreTeam"]?.ToString() ?? string.Empty,
                        Periodo = reader["Periodo"]?.ToString() ?? string.Empty,
                        TotalSesiones = sesionesCount,
                        TieneSesiones = sesionesCount > 0,
                        PrimeraSesion = reader["PrimeraSesion"] is DateTime pDate ? pDate : (DateTime?)null,
                        UltimaSesion = reader["UltimaSesion"] is DateTime uDate ? uDate : (DateTime?)null,
                        CorreoFacilitador = reader["CorreoFacilitador"]?.ToString() ?? string.Empty,
                        MotivoSinSesiones = motivo,
                        CantidadAlumnos = reader["CantidadAlumnos"] != DBNull.Value ? Convert.ToInt32(reader["CantidadAlumnos"]) : 0
                    });
                }
            }

            double cobertura = totalEquipos > 0 ? Math.Round((double)conSesiones / totalEquipos * 100.0, 1) : 0;

            return Ok(new TeamSessionsReportResponseDto
            {
                TotalEquipos = totalEquipos,
                EquiposConSesiones = conSesiones,
                EquiposSinSesiones = sinSesiones,
                PorcentajeCobertura = cobertura,
                TotalRecords = filteredTotalRecords,
                Data = items
            });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el reporte de cobertura de sesiones por equipo.");
                return StatusCode(500, new { Message = "No se pudo obtener el reporte de sesiones por equipo.", Detail = ex.Message });
            }
        }

        [HttpGet("team-sessions-report/{idSeccion}/details")]
        public async Task<ActionResult<List<TeamSessionDetailDto>>> GetTeamSessionDetails(int idSeccion)
        {
            try
            {
                var connection = _smartDbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(HttpContext.RequestAborted);
                }

                string sql = @"
                    SELECT 
                        TH.Id,
                        TH.NumeroReunion,
                        TH.Codigo,
                        TH.Fecha,
                        TH.Inicio,
                        TH.Fin,
                        TH.CorreoFacilitador,
                        TH.JoinUrl,
                        TH.Estado
                    FROM TeamsHorarios TH WITH(NOLOCK)
                    WHERE TH.IdCurso = @IdSeccion AND TH.Estado = 'A'
                    ORDER BY TH.Fecha ASC, TH.Inicio ASC;";

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                var p = cmd.CreateParameter();
                p.ParameterName = "@IdSeccion";
                p.Value = idSeccion;
                cmd.Parameters.Add(p);

                var items = new List<TeamSessionDetailDto>();
                await using (var reader = await cmd.ExecuteReaderAsync(HttpContext.RequestAborted))
                {
                    while (await reader.ReadAsync(HttpContext.RequestAborted))
                    {
                        items.Add(new TeamSessionDetailDto
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            NumeroReunion = Convert.ToInt32(reader["NumeroReunion"]),
                            Codigo = reader["Codigo"]?.ToString() ?? string.Empty,
                            Fecha = reader["Fecha"] is DateTime f ? f : (DateTime?)null,
                            Inicio = reader["Inicio"] is DateTime i ? i : (DateTime?)null,
                            Fin = reader["Fin"] is DateTime fn ? fn : (DateTime?)null,
                            CorreoFacilitador = reader["CorreoFacilitador"]?.ToString() ?? string.Empty,
                            JoinUrl = reader["JoinUrl"]?.ToString() ?? string.Empty,
                            Estado = reader["Estado"]?.ToString() ?? string.Empty
                        });
                    }
                }

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los detalles de sesiones para la sección {IdSeccion}.", idSeccion);
                return StatusCode(500, new { Message = "No se pudieron obtener las sesiones de la sección.", Detail = ex.Message });
            }
        }

        private sealed record LogsFilter(
            string? Tipo,
            string? Severidad,
            string? Entidad,
            string? Referencia,
            string? JobId,
            string? Search,
            DateTime? FechaDesde);
    }

    public class TeamSessionsReportResponseDto
    {
        public int TotalEquipos { get; set; }
        public int EquiposConSesiones { get; set; }
        public int EquiposSinSesiones { get; set; }
        public double PorcentajeCobertura { get; set; }
        public int TotalRecords { get; set; }
        public List<TeamSessionReportItemDto> Data { get; set; } = new();
    }

    public class TeamSessionReportItemDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public int IdSeccion { get; set; }
        public string CodigoSeccion { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public int TotalSesiones { get; set; }
        public bool TieneSesiones { get; set; }
        public DateTime? PrimeraSesion { get; set; }
        public DateTime? UltimaSesion { get; set; }
        public string CorreoFacilitador { get; set; } = string.Empty;
        public string MotivoSinSesiones { get; set; } = string.Empty;
        public int CantidadAlumnos { get; set; }
    }

    public class TeamSessionDetailDto
    {
        public int Id { get; set; }
        public int NumeroReunion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public DateTime? Fecha { get; set; }
        public DateTime? Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public string CorreoFacilitador { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}

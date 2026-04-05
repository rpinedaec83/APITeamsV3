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

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsReportingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly ICentralDbContext _centralDbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<TeamsReportingController> _logger;

        public TeamsReportingController(
            IMediator mediator,
            ITenantProvider tenantProvider,
            ICentralDbContext centralDbContext,
            IEncryptionService encryptionService,
            ILogger<TeamsReportingController> logger)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDbContext = centralDbContext;
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
                Rows = rows
            };

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

        private sealed record LogsFilter(
            string? Tipo,
            string? Severidad,
            string? Entidad,
            string? Referencia,
            string? JobId,
            string? Search,
            DateTime? FechaDesde);
    }
}

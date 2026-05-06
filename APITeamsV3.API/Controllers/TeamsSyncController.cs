using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.Common.Interfaces;
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
        private readonly ILogger<TeamsSyncController> _logger;

        public TeamsSyncController(
            IMediator mediator,
            ITenantProvider tenantProvider,
            ICentralDbContext centralDbContext,
            ILogger<TeamsSyncController> logger)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDbContext = centralDbContext;
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
            var tenant = _tenantProvider.GetCurrentTenant();
            var normalizedCompanyKey = (tenant.CompanyKey ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalizedCompanyKey))
            {
                return Ok(new { IsAutomaticSyncRunning = false });
            }

            var isRunning = await TryGetAutomaticSyncStatusFromStoredProcedureAsync(normalizedCompanyKey, HttpContext.RequestAborted);
            if (isRunning.HasValue)
            {
                return Ok(new { IsAutomaticSyncRunning = isRunning.Value });
            }

            var fallback = await GetAutomaticSyncStatusWithEfAsync(normalizedCompanyKey, HttpContext.RequestAborted);
            return Ok(new { IsAutomaticSyncRunning = fallback });
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

            return await _centralDbContext.SyncScheduleExecutions
                .AsNoTracking()
                .AnyAsync(
                    e => e.CompanyConfigId == companyConfigId
                         && string.Equals(e.TriggerSource, "SchedulerService", StringComparison.OrdinalIgnoreCase)
                         && e.CompletedAtUtc == null
                         && (
                             string.Equals(e.Status, "Started", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(e.Status, "Processing", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(e.Status, "Running", StringComparison.OrdinalIgnoreCase)
                         ),
                    cancellationToken);
        }
    }
}

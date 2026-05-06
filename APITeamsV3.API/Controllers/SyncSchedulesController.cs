using APITeamsV3.Application.UseCases.Schedules;
using APITeamsV3.Infrastructure.Services;
using Hangfire.Storage;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/sync-schedules")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class SyncSchedulesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICentralDbContext _centralDbContext;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;

        public SyncSchedulesController(
            IMediator mediator,
            ICentralDbContext centralDbContext,
            TenantHangfireRuntime tenantHangfireRuntime)
        {
            _mediator = mediator;
            _centralDbContext = centralDbContext;
            _tenantHangfireRuntime = tenantHangfireRuntime;
        }

        [HttpGet]
        public async Task<ActionResult<List<SyncScheduleDto>>> GetAll()
        {
            return await _mediator.Send(new GetAllSchedulesQuery(User.IsInRole("IT")));
        }

        [HttpGet("{id}/details")]
        public async Task<ActionResult<SyncScheduleDetailsDto>> GetDetails(int id)
        {
            var result = await _mediator.Send(new GetSyncScheduleDetailsQuery(id, User.IsInRole("IT")));
            if (result == null) return NotFound();

            var companyKey = await _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => c.Id == result.CompanyConfigId)
                .Select(c => c.CompanyKey)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (!string.IsNullOrWhiteSpace(companyKey))
            {
                var storage = await _tenantHangfireRuntime.GetStorageAsync(companyKey!, HttpContext.RequestAborted);

                result.Executions = result.Executions
                    .Select(execution => SyncScheduleExecutionEnricher.EnrichExecution(execution, storage))
                    .ToList();
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateSyncScheduleCommand command)
        {
            var id = await _mediator.Send(command with { AllowCrossTenant = User.IsInRole("IT") });
            if (id == 0) return Forbid();
            return Ok(id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateSyncScheduleCommand command)
        {
            if (id != command.Id) return BadRequest();
            var success = await _mediator.Send(command with { AllowCrossTenant = User.IsInRole("IT") });
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _mediator.Send(new DeleteSyncScheduleCommand(id, User.IsInRole("IT")));
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> Toggle(int id, [FromBody] ToggleRequest request)
        {
            var success = await _mediator.Send(new ToggleSyncScheduleCommand(id, request.IsEnabled, User.IsInRole("IT")));
            if (!success) return NotFound();
            return NoContent();
        }
    }

    public class ToggleRequest
    {
        public bool IsEnabled { get; set; }
    }

    internal static class SyncScheduleExecutionEnricher
    {
        public static SyncScheduleExecutionDto EnrichExecution(SyncScheduleExecutionDto execution, Hangfire.JobStorage storage)
        {
            var monitoring = storage.GetMonitoringApi();
            var jobs = execution.JobIds
                .Select(jobId => BuildJobDto(jobId, monitoring))
                .ToList();

            var succeeded = jobs.Count(job => string.Equals(job.State, "Succeeded", StringComparison.OrdinalIgnoreCase));
            var failed = jobs.Count(job => string.Equals(job.State, "Failed", StringComparison.OrdinalIgnoreCase));
            var pending = jobs.Count(job => IsPendingState(job.State));
            var summary = BuildExecutionSummary(execution, jobs, succeeded, failed, pending);

            return execution with
            {
                SucceededJobsCount = succeeded,
                FailedJobsCount = failed,
                PendingJobsCount = pending,
                ExecutionSummary = summary,
                Jobs = jobs
            };
        }

        private static SyncScheduleExecutionJobDto BuildJobDto(string jobId, IMonitoringApi monitoring)
        {
            var details = monitoring.JobDetails(jobId);
            if (details == null)
            {
                return new SyncScheduleExecutionJobDto(jobId, "Unknown", "Unknown", null, "Job data no longer available", string.Empty, null);
            }

            var state = details.History?.OrderByDescending(h => h.CreatedAt).Select(h => h.StateName).FirstOrDefault() ?? "Unknown";
            var method = details.Job?.Method?.Name ?? "Unknown";
            var args = details.Job?.Args?.Select(a => a?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();
            int? sectionId = null;

            // Heurística para extraer SectionId según el método
            if (args.Length > 0)
            {
                if (method.Contains("PilotRecordingTransfers", StringComparison.OrdinalIgnoreCase))
                {
                    // Primer arg es companyKey, no hay sectionId directo en este job "padre"
                }
                else if (int.TryParse(args[0], out var parsedSectionId))
                {
                    // Para la mayoría de los jobs de sección, el primer arg es el IdSeccion
                    sectionId = parsedSectionId;
                }
            }

            var lastState = details.History?.OrderByDescending(h => h.CreatedAt).FirstOrDefault();
            var error = string.Empty;
            var result = string.Empty;

            if (lastState != null && lastState.Data != null)
            {
                lastState.Data.TryGetValue("ExceptionMessage", out error);
                if (string.IsNullOrEmpty(error)) lastState.Data.TryGetValue("FailedReason", out error);
                
                lastState.Data.TryGetValue("Result", out result);
            }

            return new SyncScheduleExecutionJobDto(
                jobId,
                state,
                method,
                sectionId,
                error ?? string.Empty,
                result ?? string.Empty,
                details.CreatedAt);
        }

        private static string BuildExecutionSummary(
            SyncScheduleExecutionDto execution,
            List<SyncScheduleExecutionJobDto> jobs,
            int succeeded,
            int failed,
            int pending)
        {
            if (!string.IsNullOrWhiteSpace(execution.ErrorMessage))
            {
                return execution.ErrorMessage;
            }

            if (jobs.Count == 0)
            {
                return execution.Status switch
                {
                    "Succeeded" => "La ejecución terminó, pero no hay jobs asociados para inspeccionar.",
                    "Skipped" => "La ejecución fue omitida.",
                    "Failed" => "La ejecución falló antes de encolar jobs.",
                    _ => "No hay detalle de jobs para esta ejecución."
                };
            }

            if (failed > 0)
            {
                var firstFailure = jobs.FirstOrDefault(job => !string.IsNullOrWhiteSpace(job.Error));
                return firstFailure != null
                    ? $"Fallaron {failed} job(s). Primer error: {firstFailure.Error}"
                    : $"Fallaron {failed} job(s) de {jobs.Count}.";
            }

            if (pending > 0)
            {
                return $"Hay {pending} job(s) aún en cola o en proceso; {succeeded} completado(s).";
            }

            return $"Todos los jobs terminaron correctamente ({succeeded}/{jobs.Count}).";
        }

        private static bool IsPendingState(string state)
        {
            return string.Equals(state, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Processing", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Awaiting", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetStateValue(Hangfire.Storage.StateData? stateData, string key)
        {
            if (stateData?.Data == null)
            {
                return null;
            }

            return stateData.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }
    }
}

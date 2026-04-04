using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

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

        public JobsController(
            IHangfireJobService jobService,
            ITenantProvider tenantProvider,
            TenantHangfireRuntime tenantHangfireRuntime)
        {
            _jobService = jobService;
            _tenantProvider = tenantProvider;
            _tenantHangfireRuntime = tenantHangfireRuntime;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IReadOnlyList<HangfireJobSnapshotDto>>> GetRecentJobs([FromQuery] int take = 50)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            var monitoring = storage.GetMonitoringApi();
            var pageSize = Math.Clamp(take, 1, 200);

            var snapshots = new Dictionary<string, HangfireJobSnapshotDto>(StringComparer.OrdinalIgnoreCase);

            void AddSnapshot(string jobId, string state, DateTime? stateAt, Hangfire.Common.Job? job, string? error = null)
            {
                if (string.IsNullOrWhiteSpace(jobId) || snapshots.ContainsKey(jobId))
                {
                    return;
                }

                var args = job?.Args?.Select(a => a?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();
                int? idSeccion = null;
                if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
                {
                    idSeccion = parsedSection;
                }

                snapshots[jobId] = new HangfireJobSnapshotDto
                {
                    JobId = jobId,
                    State = state,
                    Method = job?.Method?.Name ?? "Unknown",
                    IdSeccion = idSeccion,
                    Arguments = args,
                    Error = error,
                    Timestamp = stateAt ?? DateTime.UtcNow
                };
            }

            foreach (var item in monitoring.ProcessingJobs(0, pageSize))
            {
                AddSnapshot(item.Key, "Processing", item.Value?.StartedAt, item.Value?.Job);
            }

            foreach (var item in monitoring.EnqueuedJobs("default", 0, pageSize))
            {
                AddSnapshot(item.Key, "Enqueued", item.Value?.EnqueuedAt, item.Value?.Job);
            }

            foreach (var item in monitoring.ScheduledJobs(0, pageSize))
            {
                AddSnapshot(item.Key, "Scheduled", item.Value?.EnqueueAt, item.Value?.Job);
            }

            foreach (var item in monitoring.FailedJobs(0, pageSize))
            {
                AddSnapshot(item.Key, "Failed", item.Value?.FailedAt, item.Value?.Job, item.Value?.ExceptionMessage ?? item.Value?.Reason);
            }

            foreach (var item in monitoring.SucceededJobs(0, pageSize))
            {
                AddSnapshot(item.Key, "Succeeded", item.Value?.SucceededAt, item.Value?.Job);
            }

            var ordered = snapshots.Values
                .OrderByDescending(x => x.Timestamp)
                .Take(pageSize)
                .ToList();

            return Ok(ordered);
        }

        [HttpPost("generate-schedule/{idSeccion}")]
        public async Task<ActionResult<string>> GenerateSchedule(int idSeccion)
        {
            var jobId = await _jobService.EnqueueGenerateSchedule(idSeccion);
            return Ok(new { JobId = jobId, Message = "Generate Schedule Job Enqueued" });
        }

        [HttpPost("sync-roster/{idSeccion}")]
        public async Task<ActionResult<string>> SyncRoster(int idSeccion, [FromQuery] bool fullSync = true)
        {
            var jobId = await _jobService.EnqueueSyncRoster(idSeccion, fullSync);
            return Ok(new { JobId = jobId, Message = "Sync Roster Job Enqueued" });
        }

        [HttpPost("sync-dates/{idSeccion}")]
        public async Task<ActionResult<string>> SyncDates(int idSeccion)
        {
            var jobId = await _jobService.EnqueueSyncDates(idSeccion);
            return Ok(new { JobId = jobId, Message = "Sync Dates Job Enqueued" });
        }

        [HttpPost("sync-facilitator/{idSeccion}")]
        public async Task<ActionResult<string>> SyncFacilitator(int idSeccion)
        {
            var jobId = await _jobService.EnqueueSyncFacilitator(idSeccion);
            return Ok(new { JobId = jobId, Message = "Sync Facilitator Job Enqueued" });
        }

        [HttpPost("update-join-url")]
        public async Task<ActionResult<string>> UpdateJoinUrl([FromBody] UpdateJoinUrlRequest request)
        {
            var jobId = await _jobService.EnqueueUpdateJoinUrl(request.IdSeccion, request.JoinUrl, request.IdEvento);
            return Ok(new { JobId = jobId, Message = "Update Join URL Job Enqueued" });
        }

        [HttpPost("sync-missing-students/{idSeccion}")]
        public async Task<ActionResult<string>> SyncMissingStudents(int idSeccion)
        {
            var jobId = await _jobService.EnqueueSyncMissingStudents(idSeccion);
            return Ok(new { JobId = jobId, Message = "Sync Missing Students Job Enqueued" });
        }

        [HttpPost("sync-obsolete-students/{idSeccion}")]
        public async Task<ActionResult<string>> SyncObsoleteStudents(int idSeccion)
        {
            var jobId = await _jobService.EnqueueSyncObsoleteStudents(idSeccion);
            return Ok(new { JobId = jobId, Message = "Sync Obsolete Students Job Enqueued" });
        }

        [HttpPost("sync-renamed-teams/{idSeccion}")]
        public async Task<ActionResult<string>> SyncRenamedTeams(int idSeccion)
        {
            var jobId = await _jobService.EnqueueSyncRenamedTeams(idSeccion);
            return Ok(new { JobId = jobId, Message = "Sync Renamed Teams Job Enqueued" });
        }
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
        public string State { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int? IdSeccion { get; set; }
        public DateTime Timestamp { get; set; }
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string? Error { get; set; }
    }
}

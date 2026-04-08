using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Services;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.EntityFrameworkCore;
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
        private readonly ICentralDbContext _centralDbContext;
        private readonly ISmartDbContext _smartDbContext;

        public JobsController(
            IHangfireJobService jobService,
            ITenantProvider tenantProvider,
            TenantHangfireRuntime tenantHangfireRuntime,
            ICentralDbContext centralDbContext,
            ISmartDbContext smartDbContext)
        {
            _jobService = jobService;
            _tenantProvider = tenantProvider;
            _tenantHangfireRuntime = tenantHangfireRuntime;
            _centralDbContext = centralDbContext;
            _smartDbContext = smartDbContext;
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

        [HttpGet("by-id/{jobId}")]
        public async Task<ActionResult<HangfireJobSnapshotDto>> GetJobById(string jobId)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            using var connection = storage.GetConnection();

            var jobData = connection.GetJobData(jobId);
            if (jobData == null)
            {
                return NotFound(new { Message = $"No se encontro el job {jobId} en Hangfire." });
            }

            var stateData = connection.GetStateData(jobId);
            var args = jobData.Job?.Args?.Select(a => a?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();
            int? idSeccion = null;
            if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
            {
                idSeccion = parsedSection;
            }

            var error = stateData?.Data != null && stateData.Data.TryGetValue("ExceptionMessage", out var exceptionMessage)
                ? exceptionMessage
                : stateData?.Reason;

            return Ok(new HangfireJobSnapshotDto
            {
                JobId = jobId,
                State = stateData?.Name ?? jobData.State ?? "Unknown",
                Method = jobData.Job?.Method?.Name ?? "Unknown",
                IdSeccion = idSeccion,
                Arguments = args,
                Error = error,
                Timestamp = jobData.CreatedAt
            });
        }

        [HttpPost("{jobId}/requeue")]
        public async Task<ActionResult<object>> RequeueJob(string jobId)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            using var connection = storage.GetConnection();

            var jobData = connection.GetJobData(jobId);
            if (jobData == null)
            {
                return NotFound(new { Message = $"No se encontro el job {jobId} en Hangfire." });
            }

            var currentState = connection.GetStateData(jobId)?.Name ?? jobData.State ?? string.Empty;
            if (!IsRequeueableState(currentState))
            {
                return BadRequest(new
                {
                    Message = $"El job {jobId} no se puede re-encolar desde el estado '{currentState}'."
                });
            }

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
                Message = $"Job {jobId} re-encolado correctamente."
            });
        }

        [HttpDelete("company-data")]
        public async Task<ActionResult<object>> PurgeCompanyData()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            var monitoring = storage.GetMonitoringApi();
            using var connection = storage.GetConnection();
            var client = new BackgroundJobClient(storage);
            var recurringManager = new RecurringJobManager(storage);

            var deletedRecurringJobs = 0;
            foreach (var recurringJob in connection.GetRecurringJobs())
            {
                recurringManager.RemoveIfExists(recurringJob.Id);
                deletedRecurringJobs++;
            }

            var deletedJobs = 0;
            deletedJobs += DeleteJobsBatch(monitoring.ProcessingJobs(0, 1000).Select(item => item.Key), client);
            deletedJobs += DeleteJobsBatch(monitoring.EnqueuedJobs("default", 0, 1000).Select(item => item.Key), client);
            deletedJobs += DeleteJobsBatch(monitoring.ScheduledJobs(0, 1000).Select(item => item.Key), client);
            deletedJobs += DeleteJobsBatch(monitoring.FailedJobs(0, 1000).Select(item => item.Key), client);
            deletedJobs += DeleteJobsBatch(monitoring.SucceededJobs(0, 1000).Select(item => item.Key), client);
            deletedJobs += DeleteJobsBatch(monitoring.DeletedJobs(0, 1000).Select(item => item.Key), client);

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

            var storage = await _tenantHangfireRuntime.GetStorageAsync(tenant.CompanyKey, HttpContext.RequestAborted);
            using var connection = storage.GetConnection();
            var monitoring = storage.GetMonitoringApi();

            var recurringJobs = connection.GetRecurringJobs()
                .OrderByDescending(job => job.CreatedAt ?? job.LastExecution ?? job.NextExecution ?? DateTime.MinValue)
                .Select(job =>
                {
                    int? idSeccion = null;
                    var args = job.Job?.Args?.Select(a => a?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();
                    if (args.Length > 0 && int.TryParse(args[0], out var parsedSection))
                    {
                        idSeccion = parsedSection;
                    }

                    var jobDetails = !string.IsNullOrWhiteSpace(job.LastJobId)
                        ? monitoring.JobDetails(job.LastJobId)
                        : null;

                    var result = BuildRecurringJobResult(job, jobDetails);

                    return new HangfireRecurringJobSnapshotDto
                    {
                        Id = job.Id,
                        Cron = job.Cron ?? string.Empty,
                        Queue = job.Queue ?? "default",
                        Method = job.Job?.Method?.Name ?? "Unknown",
                        IdSeccion = idSeccion,
                        CreatedAt = job.CreatedAt,
                        LastExecution = job.LastExecution,
                        NextExecution = job.NextExecution,
                        LastJobId = job.LastJobId ?? string.Empty,
                        LastJobState = job.LastJobState ?? string.Empty,
                        LastResult = result,
                        TimeZoneId = job.TimeZoneId ?? string.Empty,
                        Error = job.Error ?? string.Empty,
                        Removed = job.Removed
                    };
                })
                .ToList();

            return Ok(recurringJobs);
        }

        [HttpPost("recordings-transfer-pilot/run")]
        public async Task<ActionResult<object>> RunPilotRecordingTransfersNow()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return BadRequest(new { Message = "No se pudo resolver el tenant actual." });
            }

            var jobId = await _jobService.EnqueuePilotRecordingTransfers(tenant.CompanyKey);
            return Ok(new
            {
                JobId = jobId,
                Message = $"Recording transfer piloto encolado para tenant {tenant.CompanyKey}."
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
            var jobId = await _jobService.EnqueueGenerateSchedule(idSeccion);
            return Ok(new { JobId = jobId, Message = "Generate Schedule Job Enqueued" });
        }

        [HttpPost("sync-roster/{idSeccion}")]
        public async Task<ActionResult<string>> SyncRoster(int idSeccion, [FromQuery] bool fullSync = true)
        {
            var jobId = await _jobService.EnqueueSyncRoster(idSeccion, fullSync);
        return Ok(new { JobId = jobId, Message = "Sincronizacion Operativa Completa encolada" });
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

    public class HangfireRecurringJobSnapshotDto
    {
        public string Id { get; set; } = string.Empty;
        public string Cron { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int? IdSeccion { get; set; }
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
}

using APITeamsV3.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class JobsController : ControllerBase
    {
        private readonly IHangfireJobService _jobService;

        public JobsController(IHangfireJobService jobService)
        {
            _jobService = jobService;
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
}

using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using APITeamsV3.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsDiagnosticsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;

        public TeamsDiagnosticsController(IMediator mediator, TenantHangfireRuntime tenantHangfireRuntime)
        {
            _mediator = mediator;
            _tenantHangfireRuntime = tenantHangfireRuntime;
        }

        [HttpGet("missing-facilitators/{idSeccion}")]
        public async Task<ActionResult<List<CourseMissingFacilitatorDto>>> GetMissingFacilitators(int idSeccion)
        {
            var result = await _mediator.Send(new GetCoursesMissingFacilitatorQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("missing-students/{idSeccion}")]
        public async Task<ActionResult<List<MissingStudentDto>>> GetMissingStudents(int idSeccion)
        {
            var result = await _mediator.Send(new GetMissingStudentsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("obsolete-students/{idSeccion}")]
        public async Task<ActionResult<List<ObsoleteStudentDto>>> GetObsoleteStudents(int idSeccion)
        {
            var result = await _mediator.Send(new GetObsoleteStudentsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("expired-teams/{idSeccion}")]
        public async Task<ActionResult<List<ExpiredTeamDto>>> GetExpiredTeams(int idSeccion)
        {
            var result = await _mediator.Send(new GetExpiredTeamsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("renamed-teams/{idSeccion}")]
        public async Task<ActionResult<List<RenamedTeamDto>>> GetRenamedTeams(int idSeccion)
        {
            var result = await _mediator.Send(new GetRenamedTeamsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("hangfire/storage-health")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
        public async Task<ActionResult<IReadOnlyList<TenantHangfireRuntime.TenantHangfireStorageHealth>>> GetHangfireStorageHealth()
        {
            var result = await _tenantHangfireRuntime.GetStorageHealthSnapshotAsync(HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}

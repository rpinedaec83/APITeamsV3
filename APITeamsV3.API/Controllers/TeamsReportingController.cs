using APITeamsV3.Application.UseCases.Stats.Queries;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsReportingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsReportingController(IMediator mediator)
        {
            _mediator = mediator;
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
    }
}

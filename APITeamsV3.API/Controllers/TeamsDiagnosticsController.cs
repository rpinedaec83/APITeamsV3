using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
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

        public TeamsDiagnosticsController(IMediator mediator)
        {
            _mediator = mediator;
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
    }
}

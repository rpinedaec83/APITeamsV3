using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using APITeamsV3.Application.UseCases.Provisioning.Commands;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsProvisioningController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsProvisioningController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{idSeccion}")]
        public async Task<ActionResult<NextSessionDto>> GetNextSession(int idSeccion)
        {
            var query = new GetNextSessionToProvisionQuery(idSeccion);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("generate-schedule/{idSeccion}")]
        public async Task<ActionResult<bool>> GenerateSchedule(int idSeccion)
        {
            var command = new GenerateSectionScheduleCommand(idSeccion);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("sync-roster")]
        public async Task<ActionResult<bool>> SyncRoster([FromBody] SyncSessionRosterCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("update-join-url")]
        public async Task<ActionResult<bool>> UpdateJoinUrl([FromBody] UpdateSectionJoinUrlCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("sync-dates/{idSeccion}")]
        public async Task<ActionResult<bool>> SyncDates(int idSeccion)
        {
            var command = new SyncSessionDatesCommand(idSeccion);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("sync-facilitator/{idSeccion}")]
        public async Task<ActionResult<bool>> SyncFacilitator(int idSeccion)
        {
            var command = new SyncSessionFacilitatorCommand(idSeccion);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}

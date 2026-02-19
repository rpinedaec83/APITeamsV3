using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Application.UseCases.Users.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class TeamsUserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsUserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("users/sync")]
        public async Task<ActionResult> SyncUser([FromBody] SyncUserCommand command)
        {
            await _mediator.Send(command);
            return Ok(new { Message = "User synced successfully." });
        }

        [HttpPost("users/remove")]
        public async Task<ActionResult> RemoveUser([FromBody] RemoveUserCommand command)
        {
            await _mediator.Send(command);
            return Ok(new { Message = "User removed successfully." });
        }

        [HttpPost("teams/sync-facilitators")]
        public async Task<ActionResult<List<TeamFacilitatorChangeDto>>> SyncFacilitators([FromBody] SyncTeamFacilitatorsRequest request)
        {
            var result = await _mediator.Send(new SyncTeamFacilitatorsCommand(request.IdSeccion));
            return Ok(result);
        }
    }

    public class SyncTeamFacilitatorsRequest
    {
        public int IdSeccion { get; set; }
    }
}

using APITeamsV3.Application.UseCases.Teams.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/lifecycle")]
    public class TeamsLifecycleController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsLifecycleController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("soft-delete")]
        public async Task<ActionResult> SoftDelete([FromBody] SoftDeleteRequest request)
        {
            await _mediator.Send(new SoftDeleteTeamCommand(request.IdTeamsGroup));
            return Ok(new { Message = "Team soft deleted successfully." });
        }

        [HttpPost("create-record")]
        public async Task<ActionResult> CreateRecord([FromBody] CreateTeamRecordCommand command)
        {
            await _mediator.Send(command);
            return Ok(new { Message = "Team record created successfully." });
        }
    }

    public class SoftDeleteRequest
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
    }
}

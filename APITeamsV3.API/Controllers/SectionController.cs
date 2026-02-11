using APITeamsV3.Application.UseCases.Teams;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using APITeamsV3.Application.UseCases.Sections;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/sections")]
    public class SectionController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SectionController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{idSeccion}/provision-team")]
        public async Task<IActionResult> ProvisionTeam(int idSeccion, [FromBody] ProvisionTeamRequest request)
        {
            try
            {
                var jobId = await _mediator.Send(new ProvisionTeamCommand(idSeccion, request.OwnerEmail));
                return Accepted(new { JobId = jobId });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string code)
        {
            var result = await _mediator.Send(new APITeamsV3.Application.UseCases.Sections.GetSectionByCodeQuery { Code = code });
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetSections([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
             var result = await _mediator.Send(new GetSectionsQuery { Page = page, PageSize = pageSize });
             return Ok(new { 
                 Data = result, 
                 Page = page, 
                 Total = 100 // Mock total for now
             });
        }
    }

    public class ProvisionTeamRequest
    {
        public string OwnerEmail { get; set; } = string.Empty;
    }
}

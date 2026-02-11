using APITeamsV3.Application.UseCases.Students;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/students")]
    public class StudentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StudentController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string code)
        {
            var result = await _mediator.Send(new GetStudentByCodeQuery { Code = code });
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}

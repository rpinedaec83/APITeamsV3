using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using APITeamsV3.Application.UseCases.Available.Queries;
using System.Collections.Generic;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class SmartSectionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SmartSectionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<Seccion>>> GetAll()
        {
            var query = new GetAllSmartSectionsQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}

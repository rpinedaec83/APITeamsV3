using APITeamsV3.Application.UseCases.CompanyConfigs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/company-configs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class CompanyConfigsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CompanyConfigsController> _logger;

        public CompanyConfigsController(IMediator mediator, ILogger<CompanyConfigsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<CompanyConfigDto>>> GetAll()
        {
            return await _mediator.Send(new GetCompanyConfigsQuery());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyConfigDto>> GetById(int id)
        {
            var config = await _mediator.Send(new GetCompanyConfigByIdQuery(id));
            if (config == null) return NotFound();
            return config;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateCompanyConfigCommand command)
        {
            _logger.LogInformation("Creating Config: {Command}", JsonSerializer.Serialize(command));
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateCompanyConfigCommand command)
        {
            _logger.LogInformation("Updating Config ID {Id}. Payload: {Command}", id, JsonSerializer.Serialize(command));

            if (id != command.Id) 
            {
                _logger.LogWarning("ID Mismatch. URL: {Id}, Body: {CommandId}", id, command.Id);
                return BadRequest();
            }
            var success = await _mediator.Send(command);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _mediator.Send(new DeleteCompanyConfigCommand(id));
            if (!success) return NotFound();
            return NoContent();
        }
    }
}

using APITeamsV3.Application.UseCases.CompanyConfigs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
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
            var isAdminView = User.IsInRole("IT");
            return await _mediator.Send(new GetCompanyConfigsQuery(isAdminView));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyConfigDto>> GetById(int id)
        {
            var isAdminView = User.IsInRole("IT");
            var config = await _mediator.Send(new GetCompanyConfigByIdQuery(id, isAdminView));
            if (config == null) return NotFound();
            return config;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateCompanyConfigCommand command)
        {
            if (!User.IsInRole("IT"))
            {
                return Forbid();
            }

            _logger.LogInformation("Creating company config for {CompanyKey} ({DisplayName})", command.CompanyKey, command.DisplayName);
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateCompanyConfigCommand command)
        {
            _logger.LogInformation("Updating company config {Id}", id);

            if (id != command.Id) 
            {
                _logger.LogWarning("ID Mismatch. URL: {Id}, Body: {CommandId}", id, command.Id);
                return BadRequest();
            }

            var isAdminView = User.IsInRole("IT");
            var success = await _mediator.Send(command with { IsAdminView = isAdminView });
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!User.IsInRole("IT"))
            {
                return Forbid();
            }

            var isAdminView = true;
            var success = await _mediator.Send(new DeleteCompanyConfigCommand(id, isAdminView));
            if (!success) return NotFound();
            return NoContent();
        }
    }
}

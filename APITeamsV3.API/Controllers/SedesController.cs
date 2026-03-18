using APITeamsV3.Application.UseCases.Sedes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/sedes")]
    public class SedesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SedesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Get all sedes for a company configuration.
        /// </summary>
        [HttpGet("{companyConfigId}")]
        public async Task<ActionResult<List<SedeDto>>> GetByCompany(int companyConfigId)
        {
            var result = await _mediator.Send(new GetSedesByCompanyQuery(companyConfigId));
            return Ok(result);
        }

        /// <summary>
        /// Import sedes from Smart DB for a company configuration.
        /// </summary>
        [HttpPost("import/{companyConfigId}")]
        public async Task<ActionResult<List<SedeDto>>> ImportFromSmart(int companyConfigId)
        {
            var result = await _mediator.Send(new ImportSedesFromSmartCommand(companyConfigId));
            return Ok(result);
        }

        /// <summary>
        /// Toggle the active state of a sede.
        /// </summary>
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleActive(int id, [FromBody] ToggleSedeRequest request)
        {
            var success = await _mediator.Send(new ToggleSedeActiveCommand(id, request.IsActive));
            if (!success) return NotFound();
            return NoContent();
        }

        /// <summary>
        /// Delete a sede from the central DB.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _mediator.Send(new DeleteSedeCommand(id));
            if (!success) return NotFound();
            return NoContent();
        }

        /// <summary>
        /// Get active sede codes as CSV string for sync operations.
        /// </summary>
        [HttpGet("{companyConfigId}/active-codes")]
        public async Task<ActionResult<string>> GetActiveCodes(int companyConfigId)
        {
            var codes = await _mediator.Send(new GetActiveSedeCodesQuery(companyConfigId));
            return Ok(new { codes });
        }
    }

    public class ToggleSedeRequest
    {
        public bool IsActive { get; set; }
    }
}

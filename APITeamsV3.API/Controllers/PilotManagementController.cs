using APITeamsV3.Application.UseCases.Provisioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/pilot-management")]
    [Authorize(Roles = "ADMIN,IT")]
    public class PilotManagementController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PilotManagementController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene los códigos de periodos académicos disponibles en Smart DB para una empresa.
        /// </summary>
        [HttpGet("{companyConfigId}/periods")]
        public async Task<ActionResult<List<PilotPeriodDto>>> GetPeriods(int companyConfigId)
        {
            var result = await _mediator.Send(new GetPilotAvailablePeriodsQuery(companyConfigId));
            return Ok(result);
        }

        /// <summary>
        /// Obtiene secciones candidatas para el piloto desde la DB de Smart
        /// ejecutando la query: seccion -> PromocionGrupo -> Promocion -> Periodo
        /// filtrado por codigo de periodo (ej. '2026-IIE' para Zegel, '2026-II' para IDAT).
        /// </summary>
        [HttpGet("{companyConfigId}/candidates")]
        public async Task<ActionResult<List<PilotCandidateSectionDto>>> GetCandidates(
            int companyConfigId,
            [FromQuery] string periodoCodigo,
            [FromQuery] bool onlyEsTeams = false)
        {
            if (string.IsNullOrWhiteSpace(periodoCodigo))
                return BadRequest("periodoCodigo es requerido.");

            var result = await _mediator.Send(new GetPilotCandidateSectionsQuery(companyConfigId, periodoCodigo, onlyEsTeams));
            return Ok(result);
        }

        /// <summary>
        /// Obtiene las secciones actualmente registradas en el piloto para una empresa.
        /// </summary>
        [HttpGet("{companyConfigId}/sections")]
        public async Task<ActionResult<List<PilotSectionDto>>> GetCurrentSections(int companyConfigId)
        {
            var result = await _mediator.Send(new GetCurrentPilotSectionsQuery(companyConfigId));
            return Ok(result);
        }

        /// <summary>
        /// Agrega en bulk secciones al piloto de una empresa, evitando duplicados.
        /// </summary>
        [HttpPost("{companyConfigId}/sections/bulk")]
        public async Task<ActionResult<int>> BulkAdd(int companyConfigId, [FromBody] BulkAddRequest request)
        {
            var added = await _mediator.Send(new BulkAddPilotSectionsCommand(companyConfigId, request.IdSecciones, request.Periodo));
            return Ok(new { added });
        }

        /// <summary>
        /// Elimina una sección del piloto de una empresa.
        /// </summary>
        [HttpDelete("{companyConfigId}/sections/{pilotSectionId}")]
        public async Task<IActionResult> RemoveSection(int companyConfigId, int pilotSectionId)
        {
            var success = await _mediator.Send(new RemovePilotSectionCommand(companyConfigId, pilotSectionId));
            if (!success) return NotFound();
            return NoContent();
        }
    }

    public class BulkAddRequest
    {
        public List<int> IdSecciones { get; set; } = new();
        public string? Periodo { get; set; }
    }
}

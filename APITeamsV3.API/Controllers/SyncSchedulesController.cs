using APITeamsV3.Application.UseCases.Schedules;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/sync-schedules")]
    public class SyncSchedulesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SyncSchedulesController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<List<SyncScheduleDto>>> GetAll()
        {
            return await _mediator.Send(new GetAllSchedulesQuery());
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateSyncScheduleCommand command)
        {
            var id = await _mediator.Send(command);
            return Ok(id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateSyncScheduleCommand command)
        {
            if (id != command.Id) return BadRequest();
            var success = await _mediator.Send(command);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _mediator.Send(new DeleteSyncScheduleCommand(id));
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> Toggle(int id, [FromBody] ToggleRequest request)
        {
            var success = await _mediator.Send(new ToggleSyncScheduleCommand(id, request.IsEnabled));
            if (!success) return NotFound();
            return NoContent();
        }
    }

    public class ToggleRequest
    {
        public bool IsEnabled { get; set; }
    }
}

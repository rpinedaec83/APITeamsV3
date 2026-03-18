using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class TeamsSyncController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsSyncController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Refreshes Smart programming tables and returns students
        /// that need to be added to the Teams group.
        /// </summary>
        [HttpPost("missing-students/{idSeccion}")]
        public async Task<ActionResult<List<MissingStudentDto>>> SyncMissingStudents(int idSeccion)
        {
            var result = await _mediator.Send(new SyncMissingStudentsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Returns students currently in the Teams group who are
        /// no longer enrolled in the section (to be removed).
        /// </summary>
        [HttpPost("obsolete-students/{idSeccion}")]
        public async Task<ActionResult<List<ObsoleteStudentDto>>> SyncObsoleteStudents(int idSeccion)
        {
            var result = await _mediator.Send(new SyncObsoleteStudentsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Returns teams whose display name / description no longer
        /// matches the Smart naming policy (to be renamed via Graph).
        /// </summary>
        [HttpPost("renamed-teams/{idSeccion}")]
        public async Task<ActionResult<List<RenamedTeamDto>>> SyncRenamedTeams(int idSeccion)
        {
            var result = await _mediator.Send(new SyncRenamedTeamsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Syncs facilitators for a section (delegates to existing SyncTeamFacilitatorsCommand).
        /// </summary>
        [HttpPost("facilitators/{idSeccion}")]
        public async Task<ActionResult<List<TeamFacilitatorChangeDto>>> SyncFacilitators(int idSeccion)
        {
            var result = await _mediator.Send(new SyncTeamFacilitatorsCommand(idSeccion));
            return Ok(result);
        }

        /// <summary>
        /// Triggers full synchronization of all teams across all sections.
        /// Fetches all section IDs using Option 19 logic (filtered by SEDE),
        /// then enqueues chained Hangfire jobs per section to sync teams,
        /// students, facilitators, and meetings.
        /// </summary>
        [HttpPost("all")]
        public async Task<ActionResult<SyncAllTeamsResult>> SyncAll([FromQuery] string sede = "WI,SV,AP,AT,CH,IV,LN,PI,PT,SL,SM")
        {
            var result = await _mediator.Send(new SyncAllTeamsCommand(sede));
            return Ok(result);
        }
    }
}

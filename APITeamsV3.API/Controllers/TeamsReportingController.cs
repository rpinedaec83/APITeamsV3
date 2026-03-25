using APITeamsV3.Application.UseCases.Stats.Queries;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class TeamsReportingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamsReportingController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("teams-by-section/{idSeccion}")]
        public async Task<ActionResult<List<TeamBySectionDto>>> GetTeamsBySection(int idSeccion)
        {
            var result = await _mediator.Send(new GetTeamsBySectionQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("section-details/{idSeccion}")]
        public async Task<ActionResult<List<SectionDetailsDto>>> GetSectionDetails(int idSeccion)
        {
            var result = await _mediator.Send(new GetSectionDetailsQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("student-sync-status/{idSeccion}")]
        public async Task<ActionResult<List<StudentSyncStatusDto>>> GetStudentSyncStatus(int idSeccion)
        {
            var result = await _mediator.Send(new GetStudentSyncStatusQuery(idSeccion));
            return Ok(result);
        }

        [HttpGet("tenancy-stats")]
        public async Task<ActionResult<List<TenancyStatsDto>>> GetTenancyStats()
        {
            var result = await _mediator.Send(new GetTenancyStatsQuery());
            return Ok(result);
        }
        [HttpGet("logs")]
        public async Task<ActionResult<List<TeamsLogOperativoDto>>> GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? tipo = null)
        {
            var result = await _mediator.Send(new GetLogsQuery { Page = page, PageSize = pageSize, TipoFiltro = tipo });
            return Ok(result);
        }

        [HttpGet("report-schedules")]
        public async Task<ActionResult<List<ScheduleReportDto>>> GetScheduleReport()
        {
            var result = await _mediator.Send(new GetScheduleReportQuery());
            return Ok(result);
        }

        [HttpGet("report-team-members")]
        public async Task<ActionResult<List<TeamMemberReportDto>>> GetTeamMembersReport([FromQuery] string? idTeamsGroup)
        {
            var result = await _mediator.Send(new GetTeamMembersReportQuery { IdTeamsGroup = idTeamsGroup });
            return Ok(result);
        }

        [HttpGet("report-smart-vs-teams")]
        public async Task<ActionResult<List<SmartVsTeamsReportDto>>> GetSmartVsTeamsReport()
        {
            var result = await _mediator.Send(new GetSmartVsTeamsReportQuery());
            return Ok(result);
        }

        [HttpGet("report-sync-progress")]
        public async Task<ActionResult<List<SyncProgressReportDto>>> GetSyncProgressReport()
        {
            var result = await _mediator.Send(new GetSyncProgressReportQuery());
            return Ok(result);
        }
    }
}

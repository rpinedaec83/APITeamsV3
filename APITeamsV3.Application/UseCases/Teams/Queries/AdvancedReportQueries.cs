using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetScheduleReportQuery : IRequest<List<ScheduleReportDto>>
    {
    }

    public class GetTeamMembersReportQuery : IRequest<List<TeamMemberReportDto>>
    {
        public string? IdTeamsGroup { get; set; }
    }

    public class GetSmartVsTeamsReportQuery : IRequest<List<SmartVsTeamsReportDto>>
    {
    }

    public class GetSyncProgressReportQuery : IRequest<List<SyncProgressReportDto>>
    {
    }
}

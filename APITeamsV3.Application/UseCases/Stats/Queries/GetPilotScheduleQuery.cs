using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetPilotScheduleQuery : IRequest<List<PilotScheduleItemDto>>
    {
    }
}

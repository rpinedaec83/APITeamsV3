using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetTenancyStatsQuery : IRequest<List<TenancyStatsDto>>
    {
    }
}

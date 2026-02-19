using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetTeamsBySectionQuery : IRequest<List<TeamBySectionDto>>
    {
        public int IdSeccion { get; set; }

        public GetTeamsBySectionQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetExpiredTeamsQuery : IRequest<List<ExpiredTeamDto>>
    {
        public int IdSeccion { get; set; }

        public GetExpiredTeamsQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetRenamedTeamsQuery : IRequest<List<RenamedTeamDto>>
    {
        public int IdSeccion { get; set; }

        public GetRenamedTeamsQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

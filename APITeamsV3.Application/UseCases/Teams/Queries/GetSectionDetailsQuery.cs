using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetSectionDetailsQuery : IRequest<List<SectionDetailsDto>>
    {
        public int IdSeccion { get; set; }

        public GetSectionDetailsQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

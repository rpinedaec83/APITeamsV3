using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetCoursesMissingFacilitatorQuery : IRequest<List<CourseMissingFacilitatorDto>>
    {
        public int IdSeccion { get; set; }

        public GetCoursesMissingFacilitatorQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

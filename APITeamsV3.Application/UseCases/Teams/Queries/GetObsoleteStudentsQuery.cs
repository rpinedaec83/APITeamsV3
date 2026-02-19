using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetObsoleteStudentsQuery : IRequest<List<ObsoleteStudentDto>>
    {
        public int IdSeccion { get; set; }

        public GetObsoleteStudentsQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

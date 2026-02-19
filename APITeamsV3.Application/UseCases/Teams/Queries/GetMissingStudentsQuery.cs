using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetMissingStudentsQuery : IRequest<List<MissingStudentDto>>
    {
        public int IdSeccion { get; set; }

        public GetMissingStudentsQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

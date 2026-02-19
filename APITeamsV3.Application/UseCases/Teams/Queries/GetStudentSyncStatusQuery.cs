using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetStudentSyncStatusQuery : IRequest<List<StudentSyncStatusDto>>
    {
        public int IdSeccion { get; set; }

        public GetStudentSyncStatusQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

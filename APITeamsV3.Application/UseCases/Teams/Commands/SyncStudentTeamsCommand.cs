using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncStudentTeamsCommand : IRequest<List<string>>
    {
        public string CodigoAlumno { get; set; } = string.Empty;

        public SyncStudentTeamsCommand(string codigoAlumno)
        {
            CodigoAlumno = codigoAlumno;
        }
    }
}

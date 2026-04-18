using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncMissingStudentsCommand : IRequest<List<MissingStudentDto>>
    {
        public int IdSeccion { get; set; }
        public string? JobId { get; set; }

        public SyncMissingStudentsCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }
}

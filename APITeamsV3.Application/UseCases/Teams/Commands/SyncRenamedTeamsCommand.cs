using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncRenamedTeamsCommand : IRequest<List<RenamedTeamDto>>
    {
        public int IdSeccion { get; }
        public string? JobId { get; }

        public SyncRenamedTeamsCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }
}

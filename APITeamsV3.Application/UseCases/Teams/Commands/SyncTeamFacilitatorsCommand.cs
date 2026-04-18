using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncTeamFacilitatorsCommand : IRequest<List<TeamFacilitatorChangeDto>>
    {
        public int IdSeccion { get; set; }
        public string? JobId { get; set; }

        public SyncTeamFacilitatorsCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }

    public class TeamFacilitatorChangeDto
    {
        public string IdTeam { get; set; } = string.Empty;
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string ApellidosFacilitador { get; set; } = string.Empty;
        public string EmailFacilitador { get; set; } = string.Empty;
        public string OldEmailFacilitador { get; set; } = string.Empty;
        public string OldCodigoFacilitador { get; set; } = string.Empty;
    }
}

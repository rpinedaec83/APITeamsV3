using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncTeamFacilitatorsCommand : IRequest<List<TeamFacilitatorChangeDto>>
    {
        public int IdSeccion { get; set; }

        public SyncTeamFacilitatorsCommand(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }

    public class TeamFacilitatorChangeDto
    {
        public string IdTeam { get; set; } = string.Empty;
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string ApellidosFacilitador { get; set; } = string.Empty;
        public string OldCodigoFacilitador { get; set; } = string.Empty;
    }
}

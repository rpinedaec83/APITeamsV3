using MediatR;
using System;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class CreateTeamRecordCommand : IRequest<Unit>
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string Propietario1 { get; set; } = string.Empty;
        public string Propietario2 { get; set; } = string.Empty;
        public string Propietario3 { get; set; } = string.Empty;
        public string Propietario4 { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string DescripcionTeam { get; set; } = string.Empty;
        public string MailNickName { get; set; } = string.Empty;
        public int IdSeccionSmart { get; set; }
    }
}

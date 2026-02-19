using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SoftDeleteTeamCommand : IRequest<Unit>
    {
        public string IdTeamsGroup { get; set; }

        public SoftDeleteTeamCommand(string idTeamsGroup)
        {
            IdTeamsGroup = idTeamsGroup;
        }
    }
}

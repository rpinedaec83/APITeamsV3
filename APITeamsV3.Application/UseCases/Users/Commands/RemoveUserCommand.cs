using MediatR;

namespace APITeamsV3.Application.UseCases.Users.Commands
{
    public class RemoveUserCommand : IRequest<Unit>
    {
        public string IdTeamsGroup { get; set; }
        public string CodigoAlumno { get; set; }

        public RemoveUserCommand(string idTeamsGroup, string codigoAlumno)
        {
            IdTeamsGroup = idTeamsGroup;
            CodigoAlumno = codigoAlumno;
        }
    }
}

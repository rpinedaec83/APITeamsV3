using MediatR;

namespace APITeamsV3.Application.UseCases.Users.Commands
{
    public class SyncUserCommand : IRequest<Unit>
    {
        public string IdTeamsGroup { get; set; }
        public string CodigoAlumno { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Email { get; set; }
        public string Tipo { get; set; }

        public SyncUserCommand(string idTeamsGroup, string codigoAlumno, string nombres, string apellidos, string email, string tipo)
        {
            IdTeamsGroup = idTeamsGroup;
            CodigoAlumno = codigoAlumno;
            Nombres = nombres;
            Apellidos = apellidos;
            Email = email;
            Tipo = tipo;
        }
    }
}

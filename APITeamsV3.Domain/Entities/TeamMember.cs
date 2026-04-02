using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamMember
    {
        public int IdUsuario { get; set; } // PK Identity
        public string IdTeams { get; set; } = string.Empty; // FK to TeamsEquipos
        public string CodigoAlumno { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Tipo { get; set; } = "A"; // A=Alumno, F=Facilitador
        public string Estado { get; set; } = "A";
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public int UsuarioCreacion { get; set; } = 1; // System ID
        public int? UsuarioModificacion { get; set; }
    }
}

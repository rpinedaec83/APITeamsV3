using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamSession
    {
        public int IdHorarioTeams { get; set; }
        public string IdTeams { get; set; } = string.Empty;
        public int? IdCurso { get; set; }
        public string? IdEvento { get; set; }
        public int? NumeroReunion { get; set; }
        public int? IdHorario { get; set; }
        public string? Codigo { get; set; }
        public DateTime? Fecha { get; set; }
        public int? Inicio { get; set; }
        public int? Fin { get; set; }
        public string CodigoAlumno { get; set; } = string.Empty;
        public string? CorreoAlumno { get; set; }
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string? CorreoFacilitador { get; set; }
        public string Estado { get; set; } = "A";
        public string? JoinUrl { get; set; }
        public int UsuarioCreacion { get; set; } = 1;
        public DateTime FechaCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}

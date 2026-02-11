using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamSession
    {
        public int Id { get; set; } // PK Identity? (Check legacy schema)
        public string IdTeams { get; set; } = string.Empty;
        public int IdCurso { get; set; }
        public string IdEvento { get; set; } = string.Empty; // Graph Event Id
        public int NumeroReunion { get; set; }
        public int IdHorario { get; set; }
        public string Codigo { get; set; } = string.Empty; // ???
        public DateTime Fecha { get; set; }
        public int Inicio { get; set; } // Minutes from midnight? or integer representation?
        public int Fin { get; set; }
        public string JoinUrl { get; set; } = string.Empty;
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string Estado { get; set; } = "A";
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}

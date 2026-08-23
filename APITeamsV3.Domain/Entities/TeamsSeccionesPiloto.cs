using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamsSeccionesPiloto
    {
        public int IdSeccion { get; set; }
        public string? Periodo { get; set; }
        public bool EsActivo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaModificacion { get; set; }
        public string? Observacion { get; set; }
    }
}

using System;

namespace APITeamsV3.Domain.Entities
{
    public class SeccionTable
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int? IdCurso { get; set; }
        public int? IdPromocion { get; set; }
        public int? IdPeriodo { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}

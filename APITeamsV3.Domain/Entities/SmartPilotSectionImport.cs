using System;

namespace APITeamsV3.Domain.Entities
{
    /// <summary>
    /// Temporary mapping DTO for raw SQL results from Seccion/Periodo query in Smart DB.
    /// Used to fetch pilot candidate sections with date window details.
    /// </summary>
    public class SmartPilotSectionImport
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? TipoServicio { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime? PeriodoInicio { get; set; }
        public DateTime? PeriodoFin { get; set; }
        public bool? EsTeams { get; set; }
    }
}

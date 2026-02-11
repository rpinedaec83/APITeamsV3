using System;

namespace APITeamsV3.Domain.Entities
{
    public class Seccion
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int IdCurso { get; set; }
        public int IdPromocion { get; set; }
        public int IdPeriodo { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        
        // Extended Info (likely from joins or views)
        public string CursoNombre { get; set; } = string.Empty;
        public string ProductoCodigo { get; set; } = string.Empty;
        public string ProductoNombre { get; set; } = string.Empty;
        public string SedeNombre { get; set; } = string.Empty;
        public string UnidadNegocioNombre { get; set; } = string.Empty;
        public string UnidadAcademicaNombre { get; set; } = string.Empty;
        public string CodigoPeriodo { get; set; } = string.Empty;
        public string GrupoCodigo { get; set; } = string.Empty; // Sección ID String e.g. "12239"
        
        // Date Filter Support
        public string TipoServicio { get; set; } = string.Empty;
        public DateTime? PeriodoInicio { get; set; }
        public DateTime? PeriodoFin { get; set; }
        
        // Facilitator
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string EmailFacilitador { get; set; } = string.Empty;

        // Calculated for Teams
        public string CalculatedMailNickname { get; set; } = string.Empty;
        public string CalculatedDisplayName { get; set; } = string.Empty;
    }
}

using System;

namespace APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport
{
    public class DetailedReportDto
    {
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public string PeriodoCodigo { get; set; } = string.Empty;
        public DateTime? PeriodoInicio { get; set; }
        public DateTime? PeriodoFin { get; set; }
        public string Seccion { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string Curso { get; set; } = string.Empty;
        public int TotalSesiones { get; set; }
        public string DocenteCodigo { get; set; } = string.Empty;
        public string DocenteNombres { get; set; } = string.Empty;
        public string DocenteCorreo { get; set; } = string.Empty;
        public string DocenteCelular { get; set; } = string.Empty;
        public string DocenteSedePrincipal { get; set; } = string.Empty;
        public string DocenteCategoria { get; set; } = string.Empty;
        public string DocenteTipoResponsable { get; set; } = string.Empty;
        public int IdMatricula { get; set; }
        public string AlumnoCelular { get; set; } = string.Empty;
        public string Alumno { get; set; } = string.Empty;
        public string AlumnoTipoCondicion { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string DescripcionHorario { get; set; } = string.Empty;
        public bool MigroTeams { get; set; }
        public DateTime? FechaCreacionEquipoTeams { get; set; }
    }
}

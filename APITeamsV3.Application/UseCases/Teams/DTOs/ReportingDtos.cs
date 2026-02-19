using System;

namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    // Option 8: GetTeamsBySectionQuery
    public class TeamBySectionDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string Propietario1 { get; set; } = string.Empty;
        public string Propietario2 { get; set; } = string.Empty;
        public string Propietario3 { get; set; } = string.Empty;
        public string Propietario4 { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string DescripcionTeam { get; set; } = string.Empty;
        public string MailNickName { get; set; } = string.Empty;
        public string EstadoTeam { get; set; } = string.Empty;
        public int IdSeccionSmart { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public string IsActive { get; set; } = string.Empty;
    }

    // Option 24: GetSectionDetailsQuery
    public class SectionDetailsDto
    {
        public string Sede { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string PromocionCodigo { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public string CursoCodigo { get; set; } = string.Empty;
        public string CursoNombre { get; set; } = string.Empty;
        public string EstadoCursoHorario { get; set; } = string.Empty;
        public string Inicio { get; set; } = string.Empty;
        public string Fin { get; set; } = string.Empty;
        public string FacilitadorCodigo { get; set; } = string.Empty;
        public string FacilitadorNombre { get; set; } = string.Empty;
        public string Frecuencia { get; set; } = string.Empty;
        public int TotalAlumnos { get; set; }
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public int IdSeccion { get; set; }
    }

    // Option 35: GetStudentSyncStatusQuery
    public class StudentSyncStatusDto
    {
        public int IdCurso { get; set; }
        public string Sede { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string PromocionCodigo { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public string CursoCodigo { get; set; } = string.Empty;
        public string CursoNombre { get; set; } = string.Empty;
        public string EstadoCursoHorario { get; set; } = string.Empty;
        public string InicioPeriodo { get; set; } = string.Empty;
        public string FinPeriodo { get; set; } = string.Empty;
        public string FacilitadorCodigo { get; set; } = string.Empty;
        public string FacilitadorNombre { get; set; } = string.Empty;
        public string Frecuencia { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string Propietario2 { get; set; } = string.Empty;
        public string CodigoAnterior { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string CodigoTeams { get; set; } = string.Empty;
        public DateTime? FechaModificacion { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public int IdSeccion { get; set; }
        public string IdTeamsGroup { get; set; } = string.Empty;
        public DateTime? FechaInicio { get; set; }
    }

    // Option 36: GetTenancyStatsQuery
    public class TenancyStatsDto
    {
        public int IdPeriodo { get; set; }
        public string Sede { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public string UnidadNegocio { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public int Equipos { get; set; }
        public int EquiposActivos { get; set; }
        public decimal PorEquiposActivos { get; set; }
        public int Docentes { get; set; }
        public int NoDocentes { get; set; }
        public decimal PorDocente { get; set; }
        public int CursoxAlumnos { get; set; }
        public int CursoxTeams { get; set; }
        public decimal PorCursoxAlumnos { get; set; }
        public int Alumnos { get; set; }
        public int EnTeams { get; set; }
        public decimal PorAlumnos { get; set; }
    }
}

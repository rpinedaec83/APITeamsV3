using System;
using Microsoft.EntityFrameworkCore;

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
        public bool ExisteEnGraph { get; set; }
        public string GraphName { get; set; } = string.Empty;
        public string GraphDescription { get; set; } = string.Empty;
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
        [Precision(5, 2)]
        public decimal PorEquiposActivos { get; set; }
        public int Docentes { get; set; }
        public int NoDocentes { get; set; }
        [Precision(5, 2)]
        public decimal PorDocente { get; set; }
        public int CursoxAlumnos { get; set; }
        public int CursoxTeams { get; set; }
        [Precision(5, 2)]
        public decimal PorCursoxAlumnos { get; set; }
        public int Alumnos { get; set; }
        public int EnTeams { get; set; }
        [Precision(5, 2)]
        public decimal PorAlumnos { get; set; }
    }

    public class DashboardSummaryDto
    {
        public string CompanyKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsPilotMode { get; set; }
        public int PilotSectionsConfigured { get; set; }
        public string DefaultChannelName { get; set; } = string.Empty;
        public string MeetingPolicyMode { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public List<TenancyStatsDto> Rows { get; set; } = [];
    }

    public class TeamsLogOperativoDto
    {
        public int Id { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string EntidadAfectada { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string ContextoTecnico { get; set; } = string.Empty;
        public string Severidad { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }

    public class ScheduleReportDto
    {
        public int IdSeccion { get; set; }
        public string Seccion { get; set; } = string.Empty;
        public string Curso { get; set; } = string.Empty;
        public string Dia { get; set; } = string.Empty;
        public int Inicio { get; set; }
        public int Fin { get; set; }
        public string Sede { get; set; } = string.Empty;
        public string Facilitador { get; set; } = string.Empty;
    }

    public class TeamMemberReportDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string CodigoAlumno { get; set; } = string.Empty;
        public string NombreAlumno { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty; // Owner, Member
        public string Estado { get; set; } = string.Empty; // A, I
    }

    public class SmartVsTeamsReportDto
    {
        public int IdSeccion { get; set; }
        public string Seccion { get; set; } = string.Empty;
        public int AlumnosSmart { get; set; }
        public int AlumnosTeams { get; set; }
        public int Diferencia { get; set; }
        public string EstadoTeam { get; set; } = string.Empty;
    }

    public class SyncProgressReportDto
    {
        public string Sede { get; set; } = string.Empty;
        public int TotalSecciones { get; set; }
        public int SeccionesSincronizadas { get; set; }
        public decimal PorcentajeAvance { get; set; }
        public int ErroresUltimas24h { get; set; }
    }

    public class LogOperationalSummaryDto
    {
        public int StudentsSuccess { get; set; }
        public int AgendasSuccess { get; set; }
        public int TeamsSuccess { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }
    }
}

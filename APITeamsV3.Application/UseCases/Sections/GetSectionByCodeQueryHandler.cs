using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionByCodeQueryHandler : IRequestHandler<GetSectionByCodeQuery, SectionDetailDto?>
    {
        private readonly ISmartDbContext _context;
        private readonly ISectionEligibilityService _eligibilityService;
        private readonly ITenantProvider _tenantProvider;
        private readonly IGraphClientFactory _graphClientFactory;
        private readonly ILogger<GetSectionByCodeQueryHandler> _logger;

        public GetSectionByCodeQueryHandler(
            ISmartDbContext context,
            ISectionEligibilityService eligibilityService,
            ITenantProvider tenantProvider,
            IGraphClientFactory graphClientFactory,
            ILogger<GetSectionByCodeQueryHandler> logger)
        {
            _context = context;
            _eligibilityService = eligibilityService;
            _tenantProvider = tenantProvider;
            _graphClientFactory = graphClientFactory;
            _logger = logger;
        }

        public async Task<SectionDetailDto?> Handle(GetSectionByCodeQuery request, CancellationToken cancellationToken)
        {
            // 1. Fetch Section Details from View (vw_MatriculasActivas)
            var section = await _context.Set<Seccion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GrupoCodigo == request.Code || s.Codigo == request.Code, cancellationToken);

            if (section == null)
            {
                // Fallback: Search in Physical Seccion Table if not in Active view
                var sectionTable = await _context.Set<SeccionTable>()
                    .AsNoTracking()
                    .Where(s => s.Codigo == request.Code)
                    .Select(s => new SeccionTable
                    {
                        IdSeccion = s.IdSeccion,
                        Codigo = s.Codigo,
                        FechaInicio = s.FechaInicio,
                        FechaFin = s.FechaFin
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (sectionTable == null) return null;

                // Create a lightweight section object from the table data
                section = new Seccion
                {
                    IdSeccion = sectionTable.IdSeccion,
                    Codigo = sectionTable.Codigo,
                    GrupoCodigo = sectionTable.Codigo, // Use same for display if no group code available
                    FechaInicio = sectionTable.FechaInicio ?? default,
                    FechaFin = sectionTable.FechaFin ?? default,
                    CursoNombre = "Información limitada (No está en vista activa)",
                    EsTeams = false // Assuming no team if not active
                };
            }

            // 2. Check Eligibility Reason
            var tenant = _tenantProvider.GetCurrentTenant();
            var eligibility = await _eligibilityService.IsEligibleForTeamsAsync(section, tenant.CompanyKey);

            // 3. Check if Team exists
            var team = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == section.IdSeccion && t.EstadoTeam == "A", cancellationToken);

            var sectionJoinUrl = await _context.Set<SeccionHorario>()
                .AsNoTracking()
                .Where(sh => sh.IdSeccion == section.IdSeccion)
                .Select(sh => sh.UrlClaseVirtual)
                .FirstOrDefaultAsync(cancellationToken);

            var latestSessionJoinUrl = await _context.Set<TeamSession>()
                .AsNoTracking()
                .Where(th => th.IdCurso == section.IdSeccion && th.Estado == "A" && th.JoinUrl != null && th.JoinUrl != "")
                .OrderByDescending(th => th.Fecha)
                .ThenByDescending(th => th.Inicio)
                .Select(th => th.JoinUrl)
                .FirstOrDefaultAsync(cancellationToken);

            var linkGrabacion = !string.IsNullOrWhiteSpace(sectionJoinUrl)
                ? sectionJoinUrl
                : latestSessionJoinUrl;

            string? linkSharePoint = null;
            long? sharePointTotalBytes = null;
            long? sharePointUsedBytes = null;
            long? sharePointRemainingBytes = null;
            double? sharePointPercentAvailable = null;

            if (!request.SkipSharePoint && team != null && !string.IsNullOrWhiteSpace(team.IdTeamsGroup))
            {
                try
                {
                    var graphClient = await _graphClientFactory.CreateClientAsync();
                    
                    var primaryChannelTask = graphClient.Teams[team.IdTeamsGroup].PrimaryChannel.GetAsync(cancellationToken: cancellationToken);
                    var driveTask = graphClient.Groups[team.IdTeamsGroup].Drive.GetAsync(cancellationToken: cancellationToken);

                    await Task.WhenAll(primaryChannelTask, driveTask);

                    var primaryChannel = primaryChannelTask.Result;
                    if (!string.IsNullOrWhiteSpace(primaryChannel?.Id))
                    {
                        var folder = await graphClient.Teams[team.IdTeamsGroup].Channels[primaryChannel.Id].FilesFolder.GetAsync(cancellationToken: cancellationToken);
                        linkSharePoint = folder?.WebUrl;
                    }

                    var drive = driveTask.Result;
                    if (drive?.Quota != null)
                    {
                        sharePointTotalBytes = drive.Quota.Total;
                        sharePointUsedBytes = drive.Quota.Used;
                        sharePointRemainingBytes = drive.Quota.Remaining;
                        if (sharePointTotalBytes.HasValue && sharePointTotalBytes.Value > 0)
                        {
                            sharePointPercentAvailable = ((double)(sharePointRemainingBytes ?? 0) / sharePointTotalBytes.Value) * 100;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "No se pudo resolver LinkSharePoint o almacenamiento para IdSeccion={IdSeccion}, TeamId={TeamId}.",
                        section.IdSeccion,
                        team.IdTeamsGroup);
                }
            }

            // 4. Member Status Logic
            var memberIdentifiers = new HashSet<string>();
            if (team != null)
            {
                var teamMembers = await _context.Set<TeamMember>()
                    .AsNoTracking()
                    .Where(tm => tm.IdTeams == team.IdTeamsGroup && tm.Estado == "A")
                    .Select(tm => new { tm.CodigoAlumno, tm.Email })
                    .ToListAsync(cancellationToken);
                
                foreach (var member in teamMembers)
                {
                    AddIdentifierIfPresent(memberIdentifiers, member.CodigoAlumno);
                    AddIdentifierIfPresent(memberIdentifiers, member.Email);
                }
            }

            // 5. Fetch Students and Map Status using EF Core (vw_AlumnoMaster)
            var enrolledStudents = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == section.IdSeccion && ac.EsMatricula)
                .Select(ac => new
                {
                    Code = ac.Alumno != null ? ac.Alumno.Codigo : "N/A",
                    Name = ac.Alumno != null ? ac.Alumno.Nombre : "Unknown",
                    EmailInstitucion = ac.Alumno != null ? ac.Alumno.EmailInstitucion : string.Empty,
                    EmailPersonal = ac.Alumno != null ? ac.Alumno.EmailPersonal : null
                })
                .ToListAsync(cancellationToken);

            var students = enrolledStudents
                .Select(student => new StudentSummaryDto
                {
                    Code = student.Code,
                    Name = student.Name,
                    Status = team == null
                        ? "Sin Team"
                        : BuildStudentIdentifiers(student.Code, student.EmailInstitucion, student.EmailPersonal).Any(memberIdentifiers.Contains)
                            ? "En Team"
                            : "Pendiente"
                })
                .ToList();

            var replacementTeacher = string.IsNullOrWhiteSpace(section.EmailFacilitador)
                ? null
                : string.IsNullOrWhiteSpace(section.NombresFacilitador)
                    ? section.EmailFacilitador
                    : $"{section.NombresFacilitador} ({section.EmailFacilitador})";

            var promocionInfo = await _context.Database
                .SqlQueryRaw<PromocionLookupRow>(
                    "SELECT TOP(1) PromocionCodigo, PromocionNombre FROM Promocion WITH (NOLOCK) WHERE IdPromocion = {0}",
                    section.IdPromocion)
                .FirstOrDefaultAsync(cancellationToken);

            var promocionCodigo = !string.IsNullOrWhiteSpace(promocionInfo?.PromocionCodigo)
                ? promocionInfo.PromocionCodigo
                : section.IdPromocion.ToString();

            var promocionNombre = promocionInfo?.PromocionNombre ?? string.Empty;

            var teamTeacher = team == null || string.IsNullOrWhiteSpace(team.Propietario3)
                ? null
                : team.Propietario3;

            var teacherReplacementStatus =
                string.IsNullOrWhiteSpace(replacementTeacher) && string.IsNullOrWhiteSpace(teamTeacher)
                    ? "SIN DOCENTE"
                    : string.IsNullOrWhiteSpace(replacementTeacher)
                        ? "SIN DOCENTE ACADEMICO"
                        : string.IsNullOrWhiteSpace(teamTeacher)
                            ? "PENDIENTE DE ASIGNAR"
                            : string.Equals(teamTeacher, section.EmailFacilitador, System.StringComparison.OrdinalIgnoreCase)
                                ? "SIN CAMBIO"
                                : "REEMPLAZO";

            var (frecuencia, horarios) = await GetScheduleAndSessionsAsync(section.IdSeccion, linkGrabacion, cancellationToken);

            return new SectionDetailDto
            {
                IdSeccion = section.IdSeccion,
                Codigo = section.GrupoCodigo ?? section.Codigo,
                Sede = section.SedeNombre,
                Producto = section.ProductoNombre,
                Curso = section.CursoNombre,
                Profesor = $"{section.NombresFacilitador} ({section.CodigoFacilitador})",
                TeamTeacher = teamTeacher,
                ReplacementTeacher = replacementTeacher,
                TeacherReplacementStatus = teacherReplacementStatus,
                Division = section.UnidadAcademicaNombre,
                Programa = section.UnidadNegocioNombre,
                PromocionCodigo = promocionCodigo,
                PromocionNombre = promocionNombre,
                Semestre = section.CodigoPeriodo,
                UnidadNegocio = section.UnidadNegocioNombre,
                FechaInicio = section.FechaInicio == default ? null : section.FechaInicio,
                FechaFin = section.FechaFin == default ? null : section.FechaFin,
                Frecuencia = frecuencia,
                Horarios = horarios,
                LinkGrabacion = linkGrabacion,
                LinkSharePoint = linkSharePoint,
                SharePointTotalBytes = sharePointTotalBytes,
                SharePointUsedBytes = sharePointUsedBytes,
                SharePointRemainingBytes = sharePointRemainingBytes,
                SharePointPercentAvailable = sharePointPercentAvailable,
                Members = students,
                HasTeam = team != null,
                EsTeams = eligibility.IsEligible,
                IneligibilityReason = eligibility.IsEligible ? null : eligibility.Reason
            };
        }

        private async Task<(string Frecuencia, List<SectionSessionDto> Sessions)> GetScheduleAndSessionsAsync(
            int idSeccion,
            string? defaultJoinUrl,
            CancellationToken cancellationToken)
        {
            var sessions = new List<SectionSessionDto>();
            string? frecuencia = null;

            // 1. Try to get frequency pattern from dbo.gFrecuenciaSeccionHorario
            try
            {
                frecuencia = await _context.Database
                    .SqlQueryRaw<string>("SELECT dbo.gFrecuenciaSeccionHorario({0})", idSeccion)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "dbo.gFrecuenciaSeccionHorario no disponible o falló para IdSeccion={IdSeccion}", idSeccion);
            }

            // 2. Fetch sessions from HorarioSesion with Facilitador & Actor info
            try
            {
                const string sqlSessions = @"
SELECT HS.Numero,
       HS.Fecha,
       HS.Inicio,
       HS.Fin,
       HS.Estado,
       ISNULL(A.NombreCompleto, ISNULL(F.EmailInstitucion, '')) AS Docente,
       ISNULL(F.EmailInstitucion, '') AS CorreoDocente
FROM HorarioSesion HS WITH (NOLOCK)
LEFT JOIN Facilitador F WITH (NOLOCK)
    ON F.IdFacilitador = ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado)
LEFT JOIN Actor A WITH (NOLOCK)
    ON A.IdActor = F.IdFacilitador
WHERE HS.IdSeccion = {0}
  AND HS.Estado <> 'X'
ORDER BY HS.Fecha, HS.Inicio, HS.Numero;";

                var rawSessions = await _context.Database
                    .SqlQueryRaw<RawSessionRow>(sqlSessions, idSeccion)
                    .ToListAsync(cancellationToken);

                // Fetch any session JoinUrls or meeting info from TeamsHorarios if available
                var teamSessions = await _context.Set<TeamSession>()
                    .AsNoTracking()
                    .Where(th => th.IdCurso == idSeccion && th.Estado == "A")
                    .Select(th => new { th.NumeroReunion, th.Fecha, th.Inicio, th.Fin, th.JoinUrl, th.IdEvento, th.CorreoFacilitador })
                    .ToListAsync(cancellationToken);

                if (rawSessions.Count > 0)
                {
                    var now = DateTime.Now;
                    foreach (var s in rawSessions)
                    {
                        var matchingTeamSession = teamSessions.FirstOrDefault(ts =>
                            (ts.NumeroReunion.HasValue && ts.NumeroReunion.Value == s.Numero) ||
                            (ts.Fecha.HasValue && ts.Fecha.Value.Date == s.Fecha.Date && ts.Inicio == s.Inicio));

                        var joinUrl = !string.IsNullOrWhiteSpace(matchingTeamSession?.JoinUrl)
                            ? matchingTeamSession.JoinUrl
                            : defaultJoinUrl;

                        var idEvento = matchingTeamSession?.IdEvento;
                        var teacherName = !string.IsNullOrWhiteSpace(s.Docente)
                            ? s.Docente
                            : matchingTeamSession?.CorreoFacilitador ?? string.Empty;

                        var teacherEmail = !string.IsNullOrWhiteSpace(s.CorreoDocente)
                            ? s.CorreoDocente
                            : matchingTeamSession?.CorreoFacilitador ?? string.Empty;

                        var sessionDate = s.Fecha.Date;
                        var sessionStartHour = s.Inicio / 100;
                        var sessionStartMin = s.Inicio % 100;
                        var sessionEndHour = s.Fin / 100;
                        var sessionEndMin = s.Fin % 100;
                        var sessionStartDt = sessionDate.AddHours(sessionStartHour).AddMinutes(sessionStartMin);
                        var sessionEndDt = sessionDate.AddHours(sessionEndHour).AddMinutes(sessionEndMin);

                        string estadoCalculado;
                        if (s.Estado == "X")
                        {
                            estadoCalculado = "Cancelada";
                        }
                        else if (now >= sessionStartDt && now <= sessionEndDt)
                        {
                            estadoCalculado = "En curso";
                        }
                        else if (now > sessionEndDt)
                        {
                            estadoCalculado = "Realizada";
                        }
                        else
                        {
                            estadoCalculado = "Programada";
                        }

                        sessions.Add(new SectionSessionDto
                        {
                            Numero = s.Numero,
                            Fecha = s.Fecha,
                            Dia = GetSpanishDayOfWeek(s.Fecha.DayOfWeek),
                            Inicio = FormatHour(s.Inicio),
                            Fin = FormatHour(s.Fin),
                            Horario = $"{FormatHour(s.Inicio)} - {FormatHour(s.Fin)}",
                            Facilitador = teacherName,
                            CorreoFacilitador = teacherEmail,
                            Estado = estadoCalculado,
                            JoinUrl = joinUrl,
                            IdEvento = idEvento
                        });
                    }
                }
                else if (teamSessions.Count > 0)
                {
                    // Fallback from TeamsHorarios if HorarioSesion was empty
                    var distinctTeamSessions = teamSessions
                        .GroupBy(ts => new { Fecha = ts.Fecha?.Date, ts.Inicio, ts.NumeroReunion })
                        .Select(g => g.First())
                        .OrderBy(ts => ts.Fecha)
                        .ThenBy(ts => ts.Inicio)
                        .ToList();

                    var now = DateTime.Now;
                    int num = 1;
                    foreach (var ts in distinctTeamSessions)
                    {
                        var f = ts.Fecha ?? DateTime.Today;
                        var inicio = ts.Inicio ?? 0;
                        var fin = ts.Fin ?? 0;
                        var sessionStartDt = f.AddHours(inicio / 100).AddMinutes(inicio % 100);

                        sessions.Add(new SectionSessionDto
                        {
                            Numero = ts.NumeroReunion ?? num++,
                            Fecha = f,
                            Dia = GetSpanishDayOfWeek(f.DayOfWeek),
                            Inicio = FormatHour(inicio),
                            Fin = FormatHour(fin),
                            Horario = fin > 0 ? $"{FormatHour(inicio)} - {FormatHour(fin)}" : FormatHour(inicio),
                            Facilitador = ts.CorreoFacilitador,
                            CorreoFacilitador = ts.CorreoFacilitador,
                            Estado = now > sessionStartDt ? "Realizada" : "Programada",
                            JoinUrl = ts.JoinUrl ?? defaultJoinUrl,
                            IdEvento = ts.IdEvento
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al consultar sesiones para IdSeccion={IdSeccion}", idSeccion);
            }

            // 3. If frecuencia is empty, synthesize it from the sessions
            if (string.IsNullOrWhiteSpace(frecuencia) && sessions.Count > 0)
            {
                var dayTimeGroups = sessions
                    .GroupBy(s => new { s.Dia, s.Horario })
                    .Select(g => $"{g.Key.Dia} {g.Key.Horario}")
                    .Distinct();

                frecuencia = string.Join(", ", dayTimeGroups);
            }

            return (frecuencia ?? string.Empty, sessions);
        }

        private static string GetSpanishDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miércoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => dayOfWeek.ToString()
        };

        private static string FormatHour(int hhmm)
        {
            var hours = hhmm / 100;
            var minutes = hhmm % 100;
            return $"{hours:D2}:{minutes:D2}";
        }

        private sealed class RawSessionRow
        {
            public int Numero { get; set; }
            public DateTime Fecha { get; set; }
            public int Inicio { get; set; }
            public int Fin { get; set; }
            public string Estado { get; set; } = string.Empty;
            public string Docente { get; set; } = string.Empty;
            public string CorreoDocente { get; set; } = string.Empty;
        }

        private static IEnumerable<string> BuildStudentIdentifiers(string? code, string? institutionalEmail, string? personalEmail)
        {
            var identifiers = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            AddIdentifierIfPresent(identifiers, code);
            AddIdentifierIfPresent(identifiers, institutionalEmail);
            AddIdentifierIfPresent(identifiers, personalEmail);
            return identifiers;
        }

        private static void AddIdentifierIfPresent(ISet<string> identifiers, string? value)
        {
            var normalized = NormalizeIdentifier(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                identifiers.Add(normalized);
            }
        }

        private static string NormalizeIdentifier(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var at = trimmed.IndexOf('@');
            return (at >= 0 ? trimmed.Substring(0, at) : trimmed).Trim().ToLowerInvariant();
        }

        private sealed class PromocionLookupRow
        {
            public string PromocionCodigo { get; set; } = string.Empty;
            public string PromocionNombre { get; set; } = string.Empty;
        }
    }
}

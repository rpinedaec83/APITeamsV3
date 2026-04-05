using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RegenerateAgendaCommandHandler : IRequestHandler<RegenerateAgendaCommand, DiagnosticResultDto>
    {
        private readonly ISmartDbContext _context;
        private readonly ITeamAcademicoRepository _teamRepo;
        private readonly ITeamsAgendaService _agendaService;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<RegenerateAgendaCommandHandler> _logger;

        public RegenerateAgendaCommandHandler(
            ISmartDbContext context,
            ITeamAcademicoRepository teamRepo,
            ITeamsAgendaService agendaService,
            ITeamsLogOperativoRepository logRepository,
            ILogger<RegenerateAgendaCommandHandler> logger)
        {
            _context = context;
            _teamRepo = teamRepo;
            _agendaService = agendaService;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<DiagnosticResultDto> Handle(RegenerateAgendaCommand request, CancellationToken cancellationToken)
        {
            var result = new DiagnosticResultDto { IsValid = true, Summary = "Agenda regenerada." };
            TeamEntity? team = null;

            try
            {
                team = await _teamRepo.GetBySeccionIdAsync(request.IdSeccion);
                if (team == null || string.IsNullOrWhiteSpace(team.IdTeamsGroup) || team.EstadoTeam != "A")
                {
                    result.IsValid = false;
                    result.Summary = "Team local activo no encontrado.";
                    return result;
                }

                var sectionInfo = await _context.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

                if (sectionInfo == null)
                {
                    result.IsValid = false;
                    result.Summary = "Seccion no encontrada.";
                    return result;
                }

                var channelId = await _agendaService.GetPrimaryChannelIdAsync(team.IdTeamsGroup, cancellationToken);
                if (string.IsNullOrWhiteSpace(channelId))
                {
                    await LogOperativoAsync("Error", "Agenda", team.IdTeamsGroup, "No se pudo resolver el canal principal del Team.", request.JobId);
                    result.IsValid = false;
                    result.Summary = "No se pudo resolver el canal principal del Team.";
                    return result;
                }

                var futureSessions = await GetFutureSessionsAsync(request.IdSeccion, cancellationToken);
                if (futureSessions.Count == 0)
                {
                    await LogOperativoAsync("Warning", "Agenda", request.IdSeccion.ToString(), "No hay sesiones futuras para generar agendas.", request.JobId);
                    result.IsValid = false;
                    result.Summary = "No hay sesiones futuras para generar agendas.";
                    return result;
                }

                var activeStudents = await GetActiveStudentsAsync(team.IdTeamsGroup, cancellationToken);
                activeStudents = activeStudents
                    .Where(s =>
                        !string.Equals(s.CodigoAlumno, team.Propietario2, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s.CorreoAlumno, team.Propietario2, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var groupedSchedules = futureSessions
                    .GroupBy(s => new { s.Inicio, s.Fin })
                    .OrderBy(g => g.Key.Inicio)
                    .ThenBy(g => g.Key.Fin)
                    .ToList();

                var createdMeetings = new List<CreatedMeetingBlock>();
                var hasMultipleBlocks = groupedSchedules.Count > 1;

                foreach (var scheduleBlock in groupedSchedules)
                {
                    var blockSessions = scheduleBlock
                        .OrderBy(s => s.Fecha)
                        .ThenBy(s => s.Inicio)
                        .ThenBy(s => s.Numero)
                        .ToList();

                    var firstSession = blockSessions.First();
                    var firstDate = blockSessions.Min(s => s.Fecha.Date);
                    var lastDate = blockSessions.Max(s => s.Fecha.Date);
                    var firstStart = ComposeDateTime(firstDate, scheduleBlock.Key.Inicio);
                    var firstEnd = ComposeDateTime(firstDate, scheduleBlock.Key.Fin);
                    if (firstEnd <= firstStart)
                    {
                        firstEnd = firstEnd.AddDays(1);
                    }

                    var teacherEmails = blockSessions
                        .Select(s => s.CorreoFacilitador)
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .Select(email => email.Trim())
                        .Where(email => !string.Equals(email, team.Propietario2, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(sectionInfo.EmailFacilitador))
                    {
                        var defaultTeacherEmail = sectionInfo.EmailFacilitador.Trim();
                        if (!string.Equals(defaultTeacherEmail, team.Propietario2, StringComparison.OrdinalIgnoreCase)
                            && !teacherEmails.Contains(defaultTeacherEmail, StringComparer.OrdinalIgnoreCase))
                        {
                            teacherEmails.Add(defaultTeacherEmail);
                        }
                    }

                    var studentEmails = activeStudents
                        .Select(s => s.CorreoAlumno)
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .Select(email => email.Trim())
                        .Where(email => !string.Equals(email, team.Propietario2, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var attendeeEmails = teacherEmails
                        .Concat(studentEmails)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var recurrenceDays = blockSessions
                        .Select(s => s.Fecha.DayOfWeek)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();

                    var subject = BuildSubject(sectionInfo, scheduleBlock.Key.Inicio, scheduleBlock.Key.Fin, hasMultipleBlocks);
                    var htmlContent = BuildContent(sectionInfo, scheduleBlock.Key.Inicio, scheduleBlock.Key.Fin);

                    var meeting = await _agendaService.CreateRecurringChannelMeetingAsync(
                        new TeamsMeetingRequest
                        {
                            TeamId = team.IdTeamsGroup,
                            ChannelId = channelId,
                            Subject = subject,
                            HtmlContent = htmlContent,
                            FirstOccurrenceStart = firstStart,
                            FirstOccurrenceEnd = firstEnd,
                            RecurrenceStartDate = firstDate,
                            RecurrenceEndDate = lastDate,
                            RecurrenceDays = recurrenceDays,
                            RequiredAttendeeEmails = attendeeEmails,
                            PresenterEmails = teacherEmails
                        },
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(meeting.EventId))
                    {
                        throw new InvalidOperationException($"Graph no devolvio IdEvento para bloque {FormatHour(scheduleBlock.Key.Inicio)}-{FormatHour(scheduleBlock.Key.Fin)}.");
                    }

                    createdMeetings.Add(new CreatedMeetingBlock
                    {
                        EventId = meeting.EventId,
                        JoinUrl = meeting.JoinUrl ?? string.Empty,
                        Sessions = blockSessions
                    });

                    await LogOperativoAsync(
                        "Info",
                        "Agenda",
                        meeting.EventId,
                        $"Agenda recurrente creada para bloque {FormatHour(scheduleBlock.Key.Inicio)}-{FormatHour(scheduleBlock.Key.Fin)} con {blockSessions.Count} sesiones.",
                        request.JobId);
                }

                await DeleteAndDeactivatePreviousAgendasAsync(team.IdTeamsGroup, request.IdSeccion, request.JobId, cancellationToken);

                await PersistTeamsHorariosAsync(
                    team.IdTeamsGroup,
                    request.IdSeccion,
                    createdMeetings,
                    activeStudents,
                    sectionInfo,
                    cancellationToken);

                await UpdateSeccionHorarioLinkAsync(request.IdSeccion, createdMeetings, cancellationToken);

                var totalRows = createdMeetings.Sum(m => m.Sessions.Count * Math.Max(1, activeStudents.Count));
                await LogOperativoAsync(
                    "Success",
                    "Agenda",
                    team.IdTeamsGroup,
                    $"Agenda regenerada: {createdMeetings.Count} bloques, {futureSessions.Count} sesiones, {totalRows} filas TeamsHorarios.",
                    request.JobId);

                result.IsValid = true;
                result.Summary = $"Agenda regenerada: {createdMeetings.Count} bloques recurrentes creados.";
            }
            catch (Exception ex)
            {
                if (IsTransientProvisioningError(ex))
                {
                    _logger.LogWarning(ex, "Team/Group still provisioning while regenerating agenda for section {SectionId}", request.IdSeccion);
                    result.IsValid = false;
                    result.Summary = "El Team aun se esta aprovisionando en Microsoft 365. Reintenta generar agenda en unos minutos.";
                    await LogOperativoAsync(
                        "Warning",
                        "Agenda",
                        team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        ex.Message);
                    return result;
                }

                if (IsAccessDeniedError(ex))
                {
                    _logger.LogWarning(ex, "Graph access denied while regenerating agenda for section {SectionId}", request.IdSeccion);
                    result.IsValid = false;
                    result.Summary = "Graph denego acceso para agenda de canal. /groups/{id}/events requiere permiso DELEGADO Group.ReadWrite.All (Application: no soportado).";
                    await LogOperativoAsync(
                        "Error",
                        "Agenda",
                        team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        ex.Message);
                    return result;
                }

                if (IsDelegatedAgendaAuthError(ex))
                {
                    _logger.LogWarning(ex, "Delegated agenda authentication failed for section {SectionId}", request.IdSeccion);
                    result.IsValid = false;
                    result.Summary = "No se pudo autenticar agenda con cuenta tecnica delegada. Revisar AplicativosTeams y consent de permisos delegados.";
                    await LogOperativoAsync(
                        "Error",
                        "Agenda",
                        team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        ex.Message);
                    return result;
                }

                _logger.LogError(ex, "Error regenerating agenda for section {SectionId}", request.IdSeccion);
                result.IsValid = false;
                result.Summary = "Error tecnico al regenerar agenda.";
                await LogOperativoAsync("Error", "Agenda", request.IdSeccion.ToString(), ex.Message, request.JobId, ex.StackTrace ?? string.Empty);
            }

            return result;
        }

        private async Task<List<FutureSessionRow>> GetFutureSessionsAsync(int sectionId, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT HS.IdHorario,
       HS.Numero,
       HS.Fecha,
       HS.Inicio,
       HS.Fin,
       S.Codigo AS CodigoSesion,
       ISNULL(F.CodigoAnterior, '') AS CodigoFacilitador,
       ISNULL(F.EmailInstitucion, '') AS CorreoFacilitador
FROM HorarioSesion HS WITH (NOLOCK)
INNER JOIN Seccion S WITH (NOLOCK)
    ON S.IdSeccion = HS.IdSeccion
LEFT JOIN Facilitador F WITH (NOLOCK)
    ON F.IdFacilitador = ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado)
WHERE HS.IdSeccion = {0}
  AND HS.Estado <> 'X'
  AND CONVERT(date, HS.Fecha) >= CONVERT(date, GETDATE())
ORDER BY HS.Fecha, HS.Inicio, HS.Numero;";

            return await _context.Database
                .SqlQueryRaw<FutureSessionRow>(sql, sectionId)
                .ToListAsync(cancellationToken);
        }

        private async Task<List<StudentRow>> GetActiveStudentsAsync(string teamId, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT DISTINCT
       TU.CodigoAlumno,
       TU.Email AS CorreoAlumno
FROM TeamsUsuarios TU WITH (NOLOCK)
WHERE TU.IdTeams = {0}
  AND TU.Tipo = 'A'
  AND TU.Estado = 'A'
  AND ISNULL(TU.CodigoAlumno, '') <> '';";

            var students = await _context.Database
                .SqlQueryRaw<StudentRow>(sql, teamId)
                .ToListAsync(cancellationToken);

            return students
                .Where(s => !string.IsNullOrWhiteSpace(s.CodigoAlumno))
                .GroupBy(s => s.CodigoAlumno, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private async Task DeleteAndDeactivatePreviousAgendasAsync(string teamId, int sectionId, string? jobId, CancellationToken cancellationToken)
        {
            const string getEventsSql = @"
SELECT DISTINCT
       TH.IdEvento
FROM TeamsHorarios TH WITH (NOLOCK)
WHERE TH.IdTeams = {0}
  AND TH.IdCurso = {1}
  AND TH.Estado = 'A'
  AND ISNULL(TH.IdEvento, '') <> '';";

            var previousEvents = await _context.Database
                .SqlQueryRaw<EventIdRow>(getEventsSql, teamId, sectionId)
                .ToListAsync(cancellationToken);

            foreach (var eventRow in previousEvents)
            {
                if (string.IsNullOrWhiteSpace(eventRow.IdEvento))
                {
                    continue;
                }

                try
                {
                    await _agendaService.DeleteMeetingAsync(teamId, eventRow.IdEvento);
                    await LogOperativoAsync("Info", "Agenda", eventRow.IdEvento, "Reunion anterior eliminada en Graph.", jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete previous Graph meeting {EventId}", eventRow.IdEvento);
                    await LogOperativoAsync("Warning", "Agenda", eventRow.IdEvento, "No se pudo eliminar reunion previa en Graph. Se desactiva en BD.", jobId, ex.Message);
                }
            }

            const string deactivateSql = @"
UPDATE TeamsHorarios
SET Estado = 'I',
    UsuarioModificacion = 1,
    FechaModificacion = GETDATE()
WHERE IdTeams = {0}
  AND IdCurso = {1}
  AND Estado = 'A';";

            await _context.Database.ExecuteSqlRawAsync(deactivateSql, teamId, sectionId);
        }

        private async Task PersistTeamsHorariosAsync(
            string teamId,
            int sectionId,
            List<CreatedMeetingBlock> meetings,
            List<StudentRow> students,
            Seccion section,
            CancellationToken cancellationToken)
        {
            const string insertSql = @"
INSERT INTO TeamsHorarios
(
    IdTeams,
    IdEvento,
    IdHorario,
    IdCurso,
    NumeroReunion,
    Codigo,
    Fecha,
    Inicio,
    Fin,
    CodigoAlumno,
    CorreoAlumno,
    CodigoFacilitador,
    CorreoFacilitador,
    Estado,
    JoinUrl,
    UsuarioCreacion,
    FechaCreacion
)
VALUES
(
    {0},
    {1},
    {2},
    {3},
    {4},
    {5},
    {6},
    {7},
    {8},
    {9},
    {10},
    {11},
    {12},
    'A',
    {13},
    1,
    GETDATE()
);";

            foreach (var meeting in meetings)
            {
                foreach (var session in meeting.Sessions)
                {
                    var effectiveTeacherCode = !string.IsNullOrWhiteSpace(session.CodigoFacilitador)
                        ? session.CodigoFacilitador.Trim()
                        : (!string.IsNullOrWhiteSpace(section.CodigoFacilitador) ? section.CodigoFacilitador.Trim() : "DOCENTE");

                    var effectiveTeacherEmail = !string.IsNullOrWhiteSpace(session.CorreoFacilitador)
                        ? session.CorreoFacilitador.Trim()
                        : section.EmailFacilitador?.Trim();

                    var participants = ResolveParticipants(students, effectiveTeacherCode, effectiveTeacherEmail);

                    foreach (var participant in participants)
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            insertSql,
                            teamId,
                            meeting.EventId,
                            session.IdHorario,
                            sectionId,
                            session.Numero,
                            session.CodigoSesion,
                            session.Fecha,
                            session.Inicio,
                            session.Fin,
                            participant.CodigoAlumno,
                            participant.CorreoAlumno,
                            effectiveTeacherCode,
                            effectiveTeacherEmail ?? string.Empty,
                            meeting.JoinUrl);
                    }
                }
            }
        }

        private static List<StudentRow> ResolveParticipants(List<StudentRow> students, string teacherCode, string? teacherEmail)
        {
            if (students.Count > 0)
            {
                return students;
            }

            var fallbackCode = !string.IsNullOrWhiteSpace(teacherCode)
                ? teacherCode
                : (!string.IsNullOrWhiteSpace(teacherEmail) ? teacherEmail : "DOCENTE");

            return
            [
                new StudentRow
                {
                    CodigoAlumno = fallbackCode,
                    CorreoAlumno = teacherEmail ?? string.Empty
                }
            ];
        }

        private async Task UpdateSeccionHorarioLinkAsync(int sectionId, List<CreatedMeetingBlock> meetings, CancellationToken cancellationToken)
        {
            if (meetings.Count == 1 && !string.IsNullOrWhiteSpace(meetings[0].JoinUrl))
            {
                const string updateSingleSql = @"
UPDATE SeccionHorario
SET UrlClaseVirtual = {0},
    IdEvento = {1}
WHERE IdSeccion = {2};";

                await _context.Database.ExecuteSqlRawAsync(updateSingleSql, meetings[0].JoinUrl, meetings[0].EventId, sectionId);
                return;
            }

            const string clearSql = @"
UPDATE SeccionHorario
SET UrlClaseVirtual = '',
    IdEvento = NULL
WHERE IdSeccion = {0};";

            await _context.Database.ExecuteSqlRawAsync(clearSql, sectionId);
        }

        private static DateTime ComposeDateTime(DateTime date, int hhmm)
        {
            var hours = hhmm / 100;
            var minutes = hhmm % 100;
            return new DateTime(date.Year, date.Month, date.Day, hours, minutes, 0, DateTimeKind.Unspecified);
        }

        private static string FormatHour(int hhmm)
        {
            var value = Math.Max(0, hhmm);
            var hours = value / 100;
            var minutes = value % 100;
            return $"{hours:D2}:{minutes:D2}";
        }

        private static string BuildSubject(Seccion section, int inicio, int fin, bool addHourRange)
        {
            var sectionCode = !string.IsNullOrWhiteSpace(section.GrupoCodigo) ? section.GrupoCodigo : section.Codigo;
            var uniqueCode = !string.IsNullOrWhiteSpace(section.Codigo) ? section.Codigo : section.GrupoCodigo;
            var courseName = !string.IsNullOrWhiteSpace(section.CursoNombre) ? section.CursoNombre : "Clase";
            var identityTokens = new List<string> { $"SEC:{section.IdSeccion}" };

            if (!string.IsNullOrWhiteSpace(uniqueCode))
            {
                identityTokens.Add($"COD:{uniqueCode}");
            }

            if (!string.IsNullOrWhiteSpace(sectionCode) &&
                !string.Equals(sectionCode, uniqueCode, StringComparison.OrdinalIgnoreCase))
            {
                identityTokens.Add($"GRP:{sectionCode}");
            }

            var subject = $"{courseName} [{sectionCode}] [{string.Join("|", identityTokens)}]";

            if (addHourRange)
            {
                subject = $"{subject} ({FormatHour(inicio)}-{FormatHour(fin)})";
            }

            return subject;
        }

        private static string BuildContent(Seccion section, int inicio, int fin)
        {
            return
                $"<p>Sesion programada automaticamente para la seccion <b>{section.GrupoCodigo}</b>.</p>" +
                $"<p>Curso: <b>{section.CursoNombre}</b></p>" +
                $"<p>Horario: <b>{FormatHour(inicio)} - {FormatHour(fin)}</b></p>" +
                $"<p>Este evento se genera sobre el Team para que grabaciones y recursos queden en el equipo.</p>";
        }

        private static bool IsTransientProvisioningError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return (message.Contains("requested group") && message.Contains("invalid")) ||
                   (message.Contains("resource") && message.Contains("not found")) ||
                   message.Contains("does not exist") ||
                   message.Contains("mailbox") ||
                   message.Contains("not ready");
        }

        private static bool IsAccessDeniedError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return message.Contains("access is denied") ||
                   message.Contains("insufficient privileges") ||
                   message.Contains("authorization_requestdenied");
        }

        private static bool IsDelegatedAgendaAuthError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return message.Contains("no se pudo autenticar agenda con cuenta tecnica delegada") ||
                   message.Contains("aplicativosteams") ||
                   message.Contains("usernamepasswordcredential") ||
                   message.Contains("ropc") ||
                   message.Contains("invalid_grant");
        }

        private async Task LogOperativoAsync(string type, string target, string reference, string msg, string? jobId, string context = "")
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = type,
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    ContextoTecnico = context ?? string.Empty,
                    JobId = jobId,
                    Severidad = type == "Error" ? "High" : "Low",
                    Fecha = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write operational log.");
            }
        }

        private sealed class EventIdRow
        {
            public string IdEvento { get; set; } = string.Empty;
        }

        private sealed class StudentRow
        {
            public string CodigoAlumno { get; set; } = string.Empty;
            public string CorreoAlumno { get; set; } = string.Empty;
        }

        private sealed class FutureSessionRow
        {
            public int IdHorario { get; set; }
            public int Numero { get; set; }
            public DateTime Fecha { get; set; }
            public int Inicio { get; set; }
            public int Fin { get; set; }
            public string CodigoSesion { get; set; } = string.Empty;
            public string CodigoFacilitador { get; set; } = string.Empty;
            public string CorreoFacilitador { get; set; } = string.Empty;
        }

        private sealed class CreatedMeetingBlock
        {
            public string EventId { get; set; } = string.Empty;
            public string JoinUrl { get; set; } = string.Empty;
            public List<FutureSessionRow> Sessions { get; set; } = [];
        }
    }
}

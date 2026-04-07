using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
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
    public class SyncSectionAgendaCommandHandler : IRequestHandler<SyncSectionAgendaCommand, DiagnosticResultDto>
    {
        private readonly ISmartDbContext _context;
        private readonly ITeamAcademicoRepository _teamRepo;
        private readonly ITeamsAgendaService _agendaService;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<SyncSectionAgendaCommandHandler> _logger;

        public SyncSectionAgendaCommandHandler(
            ISmartDbContext context,
            ITeamAcademicoRepository teamRepo,
            ITeamsAgendaService agendaService,
            ITeamsLogOperativoRepository logRepository,
            IMediator mediator,
            ILogger<SyncSectionAgendaCommandHandler> logger)
        {
            _context = context;
            _teamRepo = teamRepo;
            _agendaService = agendaService;
            _logRepository = logRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<DiagnosticResultDto> Handle(SyncSectionAgendaCommand request, CancellationToken cancellationToken)
        {
            var result = new DiagnosticResultDto
            {
                IsValid = true,
                Summary = "Agenda sincronizada."
            };

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

                var section = await _context.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

                if (section == null)
                {
                    result.IsValid = false;
                    result.Summary = "Seccion no encontrada.";
                    return result;
                }

                var courseStartDate = section.FechaInicio == default ? DateTime.Today : section.FechaInicio.Date;
                var courseEndDate = section.FechaFin == default ? courseStartDate : section.FechaFin.Date;

                if (courseEndDate < courseStartDate)
                {
                    courseEndDate = courseStartDate;
                }

                var futureSessions = await GetFutureSessionsAsync(request.IdSeccion, courseStartDate, courseEndDate, cancellationToken);
                if (futureSessions.Count == 0)
                {
                    result.IsValid = false;
                    result.Summary = "No hay sesiones programadas dentro del rango del curso para sincronizar agenda.";
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

                var existingBlocks = await GetExistingMeetingBlocksAsync(team.IdTeamsGroup, request.IdSeccion, cancellationToken);

                if (existingBlocks.Count == 0)
                {
                    var channelId = await _agendaService.GetPrimaryChannelIdAsync(team.IdTeamsGroup, cancellationToken);
                    if (string.IsNullOrWhiteSpace(channelId))
                    {
                        result.IsValid = false;
                        result.Summary = "No se pudo resolver el canal principal del Team para crear la agenda.";
                        return result;
                    }

                    var createdBlocks = await CreateMissingBlocksAsync(
                        team.IdTeamsGroup,
                        channelId,
                        groupedSchedules
                            .Select(g => g
                                .OrderBy(s => s.Fecha)
                                .ThenBy(s => s.Inicio)
                                .ThenBy(s => s.Numero)
                                .ToList())
                            .ToList(),
                        activeStudents,
                        section,
                        courseStartDate,
                        courseEndDate,
                        cancellationToken);

                    if (createdBlocks.Count == 0)
                    {
                        result.IsValid = false;
                        result.Summary = "No hay ocurrencias pendientes dentro del rango del curso para crear agenda.";
                        return result;
                    }

                    await UpdateSeccionHorarioLinkAsync(
                        request.IdSeccion,
                        createdBlocks.Select(c => new LinkMeetingBlock
                        {
                            EventId = c.EventId,
                            JoinUrl = c.JoinUrl
                        }).ToList(),
                        cancellationToken);

                    await LogOperativoAsync(
                        "Success",
                        "Agenda",
                        team.IdTeamsGroup,
                        $"Agenda creada automaticamente: {createdBlocks.Count} bloques.",
                        request.JobId);

                    result.Summary = $"Agenda creada automaticamente: {createdBlocks.Count} bloques.";
                    return result;
                }

                await _mediator.Send(new SyncSessionFacilitatorCommand(request.IdSeccion), cancellationToken);
                await _mediator.Send(new SyncSessionDatesCommand(request.IdSeccion), cancellationToken);
                await _mediator.Send(new SyncSessionRosterCommand
                {
                    IdSeccion = request.IdSeccion,
                    Mode = SessionRosterSyncType.FullSync
                }, cancellationToken);

                existingBlocks = await GetExistingMeetingBlocksAsync(team.IdTeamsGroup, request.IdSeccion, cancellationToken);
                var existingBySlot = existingBlocks
                    .GroupBy(b => $"{b.Inicio}-{b.Fin}")
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                string? primaryChannelId = null;
                var createdMissingBlocks = new List<CreatedMeetingBlock>();
                var updatedCount = 0;
                var errorCount = 0;

                foreach (var scheduleBlock in groupedSchedules)
                {
                    var slotKey = $"{scheduleBlock.Key.Inicio}-{scheduleBlock.Key.Fin}";
                    var blockSessions = scheduleBlock
                        .OrderBy(s => s.Fecha)
                        .ThenBy(s => s.Inicio)
                        .ThenBy(s => s.Numero)
                        .ToList();

                    var teacherEmails = BuildTeacherEmails(blockSessions, section, team.Propietario2);
                    var studentEmails = activeStudents
                        .Select(s => s.CorreoAlumno)
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .Select(email => email.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var attendeeEmails = teacherEmails
                        .Concat(studentEmails)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var firstDate = blockSessions.Min(s => s.Fecha.Date);
                    var firstStart = ComposeDateTime(firstDate, scheduleBlock.Key.Inicio);
                    var firstEnd = ComposeDateTime(firstDate, scheduleBlock.Key.Fin);
                    if (firstEnd <= firstStart)
                    {
                        firstEnd = firstEnd.AddDays(1);
                    }

                    try
                    {
                        if (existingBySlot.TryGetValue(slotKey, out var existingMeeting))
                        {
                            await _agendaService.UpdateMeetingAsync(
                                new TeamsMeetingUpdateRequest
                                {
                                    TeamId = team.IdTeamsGroup,
                                    EventId = existingMeeting.EventId,
                                    JoinUrl = existingMeeting.JoinUrl,
                                    Start = firstStart,
                                    End = firstEnd,
                                    RequiredAttendeeEmails = attendeeEmails,
                                    PresenterEmails = teacherEmails
                                },
                                cancellationToken);

                            updatedCount++;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(primaryChannelId))
                            {
                                primaryChannelId = await _agendaService.GetPrimaryChannelIdAsync(team.IdTeamsGroup, cancellationToken);
                            }

                            if (string.IsNullOrWhiteSpace(primaryChannelId))
                            {
                                throw new InvalidOperationException("No se pudo resolver el canal principal del Team para crear el bloque faltante.");
                            }

                            var createdBlock = await CreateMissingBlockAsync(
                                team.IdTeamsGroup,
                                primaryChannelId,
                                scheduleBlock.Key.Inicio,
                                scheduleBlock.Key.Fin,
                                blockSessions,
                                attendeeEmails,
                                teacherEmails,
                                section,
                                courseStartDate,
                                courseEndDate,
                                cancellationToken);

                            createdMissingBlocks.Add(createdBlock);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogWarning(
                            ex,
                            "Failed to synchronize agenda block {Inicio}-{Fin} for section {SectionId}.",
                            scheduleBlock.Key.Inicio,
                            scheduleBlock.Key.Fin,
                            request.IdSeccion);

                        await LogOperativoAsync(
                            "Warning",
                            "Agenda",
                            team.IdTeamsGroup,
                            $"No se pudo sincronizar el bloque {FormatHour(scheduleBlock.Key.Inicio)}-{FormatHour(scheduleBlock.Key.Fin)}.",
                            request.JobId,
                            ex.Message);
                    }
                }

                if (createdMissingBlocks.Count > 0)
                {
                    await PersistTeamsHorariosAsync(
                        team.IdTeamsGroup,
                        request.IdSeccion,
                        createdMissingBlocks,
                        activeStudents,
                        section,
                        cancellationToken);
                }

                if (createdMissingBlocks.Count > 0)
                {
                    var allBlocks = existingBlocks
                        .Select(b => new LinkMeetingBlock
                        {
                            EventId = b.EventId,
                            JoinUrl = b.JoinUrl
                        })
                        .Concat(createdMissingBlocks.Select(c => new LinkMeetingBlock
                        {
                            EventId = c.EventId,
                            JoinUrl = c.JoinUrl
                        }))
                        .ToList();

                    await UpdateSeccionHorarioLinkAsync(request.IdSeccion, allBlocks, cancellationToken);
                }

                result.IsValid = errorCount == 0;
                result.Summary = $"Agenda sincronizada: actualizados={updatedCount}, creados={createdMissingBlocks.Count}, errores={errorCount}.";

                await LogOperativoAsync(
                    result.IsValid ? "Success" : "Warning",
                    "Agenda",
                    team.IdTeamsGroup,
                    result.Summary,
                    request.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing agenda automatically for section {SectionId}", request.IdSeccion);
                result.IsValid = false;
                result.Summary = "Error tecnico al sincronizar agenda automaticamente.";
                await LogOperativoAsync(
                    "Error",
                    "Agenda",
                    team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                    ex.Message,
                    request.JobId,
                    ex.StackTrace ?? string.Empty);
            }

            return result;
        }

        private async Task<List<CreatedMeetingBlock>> CreateMissingBlocksAsync(
            string teamId,
            string channelId,
            IReadOnlyCollection<List<FutureSessionRow>> groupedSchedules,
            List<StudentRow> activeStudents,
            Seccion section,
            DateTime courseStartDate,
            DateTime courseEndDate,
            CancellationToken cancellationToken)
        {
            var createdBlocks = new List<CreatedMeetingBlock>();

            foreach (var blockSessions in groupedSchedules)
            {
                var inicio = blockSessions[0].Inicio;
                var fin = blockSessions[0].Fin;

                var teacherEmails = BuildTeacherEmails(blockSessions, section, null);
                var studentEmails = activeStudents
                    .Select(s => s.CorreoAlumno)
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var attendeeEmails = teacherEmails
                    .Concat(studentEmails)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                createdBlocks.Add(await CreateMissingBlockAsync(
                    teamId,
                    channelId,
                    inicio,
                    fin,
                    blockSessions,
                    attendeeEmails,
                    teacherEmails,
                    section,
                    courseStartDate,
                    courseEndDate,
                    cancellationToken));
            }

            await PersistTeamsHorariosAsync(teamId, section.IdSeccion, createdBlocks, activeStudents, section, cancellationToken);
            return createdBlocks;
        }

        private async Task<CreatedMeetingBlock> CreateMissingBlockAsync(
            string teamId,
            string channelId,
            int inicio,
            int fin,
            List<FutureSessionRow> blockSessions,
            IReadOnlyCollection<string> attendeeEmails,
            IReadOnlyCollection<string> teacherEmails,
            Seccion section,
            DateTime courseStartDate,
            DateTime courseEndDate,
            CancellationToken cancellationToken)
        {
            var recurrenceDays = blockSessions
                .Select(s => s.Fecha.DayOfWeek)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (!TryResolveFirstOccurrenceDate(courseStartDate, courseEndDate, recurrenceDays, out var firstOccurrenceDate))
            {
                throw new InvalidOperationException($"No hay ocurrencias pendientes para el bloque {FormatHour(inicio)}-{FormatHour(fin)} dentro del rango del curso.");
            }

            var firstStart = ComposeDateTime(firstOccurrenceDate, inicio);
            var firstEnd = ComposeDateTime(firstOccurrenceDate, fin);
            if (firstEnd <= firstStart)
            {
                firstEnd = firstEnd.AddDays(1);
            }

            var subject = BuildSubject(section, inicio, fin, true);
            var htmlContent = BuildContent(section, inicio, fin);

            var meeting = await _agendaService.CreateRecurringChannelMeetingAsync(
                new TeamsMeetingRequest
                {
                    TeamId = teamId,
                    ChannelId = channelId,
                    Subject = subject,
                    HtmlContent = htmlContent,
                    FirstOccurrenceStart = firstStart,
                    FirstOccurrenceEnd = firstEnd,
                    RecurrenceStartDate = firstOccurrenceDate,
                    RecurrenceEndDate = courseEndDate,
                    RecurrenceDays = recurrenceDays,
                    RequiredAttendeeEmails = attendeeEmails,
                    PresenterEmails = teacherEmails
                },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(meeting.EventId))
            {
                throw new InvalidOperationException($"Graph no devolvio IdEvento para bloque {FormatHour(inicio)}-{FormatHour(fin)}.");
            }

            return new CreatedMeetingBlock
            {
                EventId = meeting.EventId,
                JoinUrl = meeting.JoinUrl ?? string.Empty,
                Sessions = blockSessions
            };
        }

        private async Task<List<FutureSessionRow>> GetFutureSessionsAsync(int sectionId, DateTime courseStartDate, DateTime courseEndDate, CancellationToken cancellationToken)
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
  AND CONVERT(date, HS.Fecha) BETWEEN CONVERT(date, {1}) AND CONVERT(date, {2})
ORDER BY HS.Fecha, HS.Inicio, HS.Numero;";

            return await _context.Database
                .SqlQueryRaw<FutureSessionRow>(sql, sectionId, courseStartDate, courseEndDate)
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

        private async Task<List<ExistingMeetingBlock>> GetExistingMeetingBlocksAsync(
            string teamId,
            int sectionId,
            CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT DISTINCT
       TH.IdEvento AS EventId,
       ISNULL(TH.JoinUrl, '') AS JoinUrl,
       TH.Inicio,
       TH.Fin
FROM TeamsHorarios TH WITH (NOLOCK)
WHERE TH.IdTeams = {0}
  AND TH.IdCurso = {1}
  AND TH.Estado = 'A'
  AND ISNULL(TH.IdEvento, '') <> ''
  AND CONVERT(date, TH.Fecha) >= CONVERT(date, GETDATE());";

            return await _context.Database
                .SqlQueryRaw<ExistingMeetingBlock>(sql, teamId, sectionId)
                .ToListAsync(cancellationToken);
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

        private async Task UpdateSeccionHorarioLinkAsync(
            int sectionId,
            List<LinkMeetingBlock> meetings,
            CancellationToken cancellationToken)
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

        private static List<string> BuildTeacherEmails(
            IEnumerable<FutureSessionRow> blockSessions,
            Seccion section,
            string? technicalOwnerEmail)
        {
            var teacherEmails = blockSessions
                .Select(s => s.CorreoFacilitador)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Where(email => !string.Equals(email, technicalOwnerEmail, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(section.EmailFacilitador))
            {
                var defaultTeacherEmail = section.EmailFacilitador.Trim();
                if (!string.Equals(defaultTeacherEmail, technicalOwnerEmail, StringComparison.OrdinalIgnoreCase) &&
                    !teacherEmails.Contains(defaultTeacherEmail, StringComparer.OrdinalIgnoreCase))
                {
                    teacherEmails.Add(defaultTeacherEmail);
                }
            }

            return teacherEmails;
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

        private static bool TryResolveFirstOccurrenceDate(
            DateTime courseStartDate,
            DateTime courseEndDate,
            IReadOnlyCollection<DayOfWeek> recurrenceDays,
            out DateTime firstOccurrenceDate)
        {
            firstOccurrenceDate = default;

            if (recurrenceDays.Count == 0)
            {
                return false;
            }

            var searchStart = DateTime.Today > courseStartDate ? DateTime.Today : courseStartDate;
            for (var date = searchStart.Date; date <= courseEndDate.Date; date = date.AddDays(1))
            {
                if (recurrenceDays.Contains(date.DayOfWeek))
                {
                    firstOccurrenceDate = date;
                    return true;
                }
            }

            return false;
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

        private sealed class ExistingMeetingBlock
        {
            public string EventId { get; set; } = string.Empty;
            public string JoinUrl { get; set; } = string.Empty;
            public int Inicio { get; set; }
            public int Fin { get; set; }
        }

        private sealed class CreatedMeetingBlock
        {
            public string EventId { get; set; } = string.Empty;
            public string JoinUrl { get; set; } = string.Empty;
            public List<FutureSessionRow> Sessions { get; set; } = [];
        }

        private sealed class LinkMeetingBlock
        {
            public string EventId { get; set; } = string.Empty;
            public string JoinUrl { get; set; } = string.Empty;
        }
    }
}

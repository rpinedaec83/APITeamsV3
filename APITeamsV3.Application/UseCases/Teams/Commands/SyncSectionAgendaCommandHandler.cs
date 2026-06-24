using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.Kiota.Abstractions;
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
        private const string ChangeMarker = "CHG-2026-04-12-PILOT-GUARD-IDEMPOTENT-TEAMS-HORARIOS";
        private readonly ISmartDbContext _context;
        private readonly ICentralDbContext _centralContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITeamAcademicoRepository _teamRepo;
        private readonly ITeamsAgendaService _agendaService;
        private readonly IGraphClientFactory _graphClientFactory;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<SyncSectionAgendaCommandHandler> _logger;
        private readonly ICurrentUserService _currentUserService;

        public SyncSectionAgendaCommandHandler(
            ISmartDbContext context,
            ICentralDbContext centralContext,
            ITenantProvider tenantProvider,
            ITeamAcademicoRepository teamRepo,
            ITeamsAgendaService agendaService,
            IGraphClientFactory graphClientFactory,
            ITeamsLogOperativoRepository logRepository,
            IMediator mediator,
            ILogger<SyncSectionAgendaCommandHandler> logger,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _centralContext = centralContext;
            _tenantProvider = tenantProvider;
            _teamRepo = teamRepo;
            _agendaService = agendaService;
            _graphClientFactory = graphClientFactory;
            _logRepository = logRepository;
            _mediator = mediator;
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<DiagnosticResultDto> Handle(SyncSectionAgendaCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "{ChangeMarker}: SyncSectionAgenda iniciado para IdSeccion={IdSeccion}.",
                ChangeMarker,
                request.IdSeccion);

            var result = new DiagnosticResultDto
            {
                IsValid = true,
                Summary = "Agenda sincronizada."
            };

            TeamEntity? team = null;
            Seccion? section = null;

            try
            {
                team = await _teamRepo.GetBySeccionIdAsync(request.IdSeccion);
                if (team == null || string.IsNullOrWhiteSpace(team.IdTeamsGroup) || team.EstadoTeam != "A")
                {
                    result.IsValid = false;
                    result.Summary = "Team local activo no encontrado.";
                    return result;
                }

                var graphClient = await _graphClientFactory.CreateClientAsync();
                if (!await GraphGroupGuard.GroupExistsAsync(graphClient, team.IdTeamsGroup, cancellationToken))
                {
                    team.EstadoTeam = "I";
                    team.FechaModificacion = DateTime.UtcNow;
                    await _teamRepo.UpdateAsync(team);

                    result.IsValid = false;
                    result.Summary = "El Team local apunta a un grupo inexistente en Graph. Se marco como inactivo.";
                    await LogOperativoAsync("Error", "Agenda", section?.Codigo ?? team.IdTeamsGroup, result.Summary, request.JobId, request.ExecutedBy);
                    return result;
                }

                section = await _context.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

                if (section == null)
                {
                    result.IsValid = false;
                    result.Summary = "Seccion no encontrada.";
                    return result;
                }

                if (!await IsSectionAllowedInPilotModeAsync(request.IdSeccion, cancellationToken))
                {
                    _logger.LogInformation(
                        "{ChangeMarker}: Seccion {IdSeccion} omitida por modo piloto (fuera de piloto).",
                        ChangeMarker,
                        request.IdSeccion);

                    result.IsValid = false;
                    result.Summary = "Seccion omitida: no pertenece al piloto configurado.";
                    await LogOperativoAsync(
                        "Warning",
                        "Agenda",
                        section?.Codigo ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        request.ExecutedBy);
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
                        section.Codigo,
                        $"Agenda creada automaticamente: {createdBlocks.Count} bloques.",
                        request.JobId,
                        request.ExecutedBy);

                    result.Summary = $"Agenda creada automaticamente: {createdBlocks.Count} bloques.";
                    return result;
                }

                await _mediator.Send(new SyncSessionFacilitatorCommand(request.IdSeccion, request.JobId), cancellationToken);
                await _mediator.Send(new SyncSessionDatesCommand(request.IdSeccion, request.JobId), cancellationToken);
                await _mediator.Send(new SyncSessionRosterCommand
                {
                    IdSeccion = request.IdSeccion,
                    Mode = SessionRosterSyncType.FullSync,
                    JobId = request.JobId
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
                            try
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
                            catch (Exception ex) when (IsMeetingNotFoundError(ex))
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Meeting {EventId} for slot {Inicio}-{Fin} in section {SectionId} not found in Teams storage. Inactivating local link to force re-creation.",
                                    existingMeeting.EventId,
                                    scheduleBlock.Key.Inicio,
                                    scheduleBlock.Key.Fin,
                                    request.IdSeccion);

                                await LogOperativoAsync(
                                    "Warning",
                                    "Agenda",
                                    section.Codigo,
                                    $"Aviso: El bloque {FormatHour(scheduleBlock.Key.Inicio)}-{FormatHour(scheduleBlock.Key.Fin)} desapareció de Teams. Acción: El sistema lo está recreando automáticamente ahora.",
                                    request.JobId,
                                    request.ExecutedBy,
                                    ex.Message);

                                await InactivateTeamsHorarioByEventIdAsync(team.IdTeamsGroup, existingMeeting.EventId, cancellationToken);


                                // Fallback: try to re-create the block in this same run
                                if (string.IsNullOrWhiteSpace(primaryChannelId))
                                {
                                    primaryChannelId = await _agendaService.GetPrimaryChannelIdAsync(team.IdTeamsGroup, cancellationToken);
                                }

                                if (string.IsNullOrWhiteSpace(primaryChannelId))
                                {
                                    throw new InvalidOperationException("No se pudo resolver el canal principal del Team para recrear el bloque desaparecido.");
                                }

                                var recreatedBlock = await CreateMissingBlockAsync(
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

                                createdMissingBlocks.Add(recreatedBlock);
                            }
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
                            section.Codigo,
                            $"No se pudo sincronizar el bloque {FormatHour(scheduleBlock.Key.Inicio)}-{FormatHour(scheduleBlock.Key.Fin)}.",
                            request.JobId,
                            request.ExecutedBy,
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
                    section.Codigo,
                    result.Summary,
                    request.JobId,
                    request.ExecutedBy);
            }
            catch (Exception ex)
            {
                if (IsTransientProvisioningError(ex))
                {
                    _logger.LogWarning(ex, "Team/Group still provisioning while syncing agenda for section {SectionId}", request.IdSeccion);
                    result.IsValid = false;
                    result.Summary = "El Team aun se esta aprovisionando en Microsoft 365. Reintenta sincronizar agenda en unos minutos.";
                    await LogOperativoAsync(
                        "Warning",
                        "Agenda",
                        section?.Codigo ?? team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        request.ExecutedBy,
                        ex.Message);
                    return result;
                }

                if (IsAccessDeniedError(ex))
                {
                    _logger.LogWarning(ex, "Graph access denied while syncing agenda for section {SectionId}", request.IdSeccion);
                    result.IsValid = false;
                    result.Summary = "Graph denego acceso para agenda de canal. Revisar permisos delegados para operaciones de calendario del grupo.";
                    await LogOperativoAsync(
                        "Error",
                        "Agenda",
                        section?.Codigo ?? team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                        result.Summary,
                        request.JobId,
                        request.ExecutedBy,
                        ex.Message);
                    return result;
                }

                _logger.LogError(ex, "Error synchronizing agenda automatically for section {SectionId}", request.IdSeccion);
                result.IsValid = false;
                result.Summary = "Error tecnico al sincronizar agenda automaticamente.";
                await LogOperativoAsync(
                    "Error",
                    "Agenda",
                    section?.Codigo ?? team?.IdTeamsGroup ?? request.IdSeccion.ToString(),
                    ex.Message,
                    request.JobId,
                    request.ExecutedBy,
                    ex.StackTrace ?? string.Empty);
            }

            return result;
        }

        private static bool IsMeetingNotFoundError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return message.Contains("object was not found") ||
                   message.Contains("itemnotfound") ||
                   (ex is ApiException apiEx && apiEx.ResponseStatusCode == 404);
        }

        private static bool IsTransientProvisioningError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return (message.Contains("requested group") && message.Contains("invalid")) ||
                   (message.Contains("resource") && message.Contains("not found")) ||
                   message.Contains("does not exist") ||
                   message.Contains("mailbox") ||
                   message.Contains("not ready") ||
                   message.Contains("primary channel");
        }

        private static bool IsAccessDeniedError(Exception ex)
        {
            var message = (ex.Message ?? string.Empty).ToLowerInvariant();
            return message.Contains("access is denied") ||
                   message.Contains("access denied") ||
                   message.Contains("insufficient privileges") ||
                   message.Contains("authorization_requestdenied");
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
IF NOT EXISTS
(
    SELECT 1
    FROM TeamsHorarios WITH (NOLOCK)
    WHERE IdTeams = {0}
      AND IdEvento = {1}
      AND IdHorario = {2}
      AND IdCurso = {3}
      AND NumeroReunion = {4}
      AND Estado = 'A'
)
BEGIN
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
    {14},
    GETDATE()
);
END;";
            var insertedRows = 0;
            var skippedDuplicates = 0;

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
                        "SECTION", // CodigoAlumno: Grabar solo bloque
                        "N/A",     // CorreoAlumno: Grabar solo bloque
                        effectiveTeacherCode,
                        effectiveTeacherEmail ?? string.Empty,
                        meeting.JoinUrl,
                        _currentUserService.UserIdInt ?? 99);

                    insertedRows++;
                }
            }

            _logger.LogInformation(
                "{ChangeMarker}: PersistTeamsHorarios section={SectionId}, inserted={InsertedRows}, duplicatesSkipped={SkippedDuplicates}.",
                ChangeMarker,
                sectionId,
                insertedRows,
                skippedDuplicates);
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

        private async Task<bool> IsSectionAllowedInPilotModeAsync(int sectionId, CancellationToken cancellationToken)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return true;
            }

            var normalizedCompanyKey = tenant.CompanyKey.Trim().ToLowerInvariant();
            var companyConfig = await _centralContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey,
                    cancellationToken);

            if (companyConfig?.IsPilotMode != true)
            {
                return true;
            }

            return companyConfig.PilotSections.Any(ps => ps.IdSeccion == sectionId);
        }

        private async Task UpdateSeccionHorarioLinkAsync(
            int sectionId,
            List<LinkMeetingBlock> meetings,
            CancellationToken cancellationToken)
        {
            if (meetings.Count > 0 && !string.IsNullOrWhiteSpace(meetings[0].JoinUrl))
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

        private async Task InactivateTeamsHorarioByEventIdAsync(string teamId, string eventId, CancellationToken cancellationToken)
        {
            const string sql = @"
UPDATE TeamsHorarios 
SET Estado = 'I', 
    UsuarioModificacion = {2}, 
    FechaModificacion = GETDATE() 
WHERE IdTeams = {0} AND IdEvento = {1} AND Estado = 'A'";

            await _context.Database.ExecuteSqlRawAsync(sql, [teamId, eventId, _currentUserService.UserIdInt ?? 99], cancellationToken);
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

            var searchStart = courseStartDate;
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

        private async Task LogOperativoAsync(string type, string target, string reference, string msg, string? jobId, string? executedBy = null, string context = "")
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
                    Usuario = executedBy,
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


using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

            try
            {
                var team = await _teamRepo.GetBySeccionIdAsync(request.IdSeccion);
                if (team == null)
                {
                    result.IsValid = false; result.Summary = "Team local no encontrado.";
                    return result;
                }

                var sectionInfo = await _context.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);
                
                if (sectionInfo == null)
                {
                    result.IsValid = false; result.Summary = "Sección no encontrada.";
                    return result;
                }

                // 1. Invalidar agendas anteriores
                var previousSessions = await _context.Set<TeamSession>()
                    .Where(ts => ts.IdTeams == team.IdTeamsGroup && ts.Estado == "A")
                    .ToListAsync(cancellationToken);

                foreach (var session in previousSessions)
                {
                    if (!string.IsNullOrEmpty(session.IdEvento))
                    {
                        try
                        {
                            await _agendaService.DeleteMeetingAsync(team.IdTeamsGroup, session.IdEvento);
                            await LogOperativoAsync("Info", "Agenda", session.IdEvento, "Reunión anterior eliminada en Graph.", request.JobId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete previous meeting {session.IdEvento}. Still marking inactive logically.");
                        }
                    }

                    session.Estado = "I";
                    session.FechaModificacion = DateTime.UtcNow;
                }
                
                // 2. Obtener canal General
                var channelId = await _agendaService.GetPrimaryChannelIdAsync(team.IdTeamsGroup);
                if (string.IsNullOrEmpty(channelId))
                {
                    await LogOperativoAsync("Error", "Agenda", team.IdTeamsGroup, "No se encontró el canal General.", request.JobId);
                    result.IsValid = false; result.Summary = "Error al resolver Canal General.";
                    return result; // Or continue fallback? Rules dictate channel meeting.
                }

                // 3. Crear nueva reunión en Graph (Placeholder lógico por simplificación de Fechas vs Recurring)
                // In production this loops over 'HorarioSesion' calculating recurrence. We create a master meeting.
                string subject = $"Clase: {sectionInfo.CursoNombre} ({sectionInfo.GrupoCodigo})";
                string content = $"Reunión oficial de clases para la sección {sectionInfo.GrupoCodigo}.";

                var eventId = await _agendaService.CreateChannelMeetingAsync(
                    team.IdTeamsGroup, 
                    channelId, 
                    subject, 
                    content, 
                    sectionInfo.FechaInicio, 
                    sectionInfo.FechaFin);

                // 4. Persistir nueva agenda localmente
                var newSession = new TeamSession
                {
                    IdTeams = team.IdTeamsGroup,
                    IdCurso = request.IdSeccion,
                    IdEvento = eventId,
                    NumeroReunion = 1,
                    Fecha = DateTime.UtcNow.Date,
                    Inicio = 0, Fin = 0, // Should map from SeccionHorario
                    JoinUrl = string.Empty, // Populate if event allows reading OnlineMeeting URL
                    CodigoFacilitador = sectionInfo.CodigoFacilitador,
                    Estado = "A",
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Set<TeamSession>().Add(newSession);
                await _context.SaveChangesAsync(cancellationToken);
                
                await LogOperativoAsync("Success", "Agenda", eventId, "Reunión de canal regenerada correctamente.", request.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating agenda.");
                result.IsValid = false;
                result.Summary = "Error técnico al regenerar.";
                await LogOperativoAsync("Error", "Agenda", request.IdSeccion.ToString(), ex.Message, request.JobId, ex.StackTrace);
            }

            return result;
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
                _logger.LogWarning(ex, "Failed to write operational log. This might be due to a missing LogOperativo table.");
            }
        }
    }
}

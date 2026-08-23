using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncObsoleteTeamsCommandHandler : IRequestHandler<SyncObsoleteTeamsCommand, SyncObsoleteTeamsResult>
    {
        private readonly ISmartDbContext _smartDbContext;
        private readonly IGraphClientFactory _graphFactory;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<SyncObsoleteTeamsCommandHandler> _logger;

        public SyncObsoleteTeamsCommandHandler(
            ISmartDbContext smartDbContext,
            IGraphClientFactory graphFactory,
            ITeamsLogOperativoRepository logRepository,
            ILogger<SyncObsoleteTeamsCommandHandler> logger)
        {
            _smartDbContext = smartDbContext;
            _graphFactory = graphFactory;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<SyncObsoleteTeamsResult> Handle(SyncObsoleteTeamsCommand request, CancellationToken cancellationToken)
        {
            var activeTeamsQuery = _smartDbContext.TeamsEquipos
                .AsNoTracking()
                .Where(t => t.EstadoTeam == "A" || t.IsActive == "A");

            if (request.IdSeccion.HasValue && request.IdSeccion.Value > 0)
            {
                activeTeamsQuery = activeTeamsQuery.Where(t => t.IdSeccionSmart == request.IdSeccion.Value);
            }

            var activeTeams = await activeTeamsQuery.ToListAsync(cancellationToken);

            var existingSections = await _smartDbContext.SeccionTable
                .AsNoTracking()
                .Select(s => new { s.IdSeccion, s.FechaFin })
                .ToListAsync(cancellationToken);

            var existingSectionIds = existingSections.Select(s => s.IdSeccion).ToHashSet();

            int fechaFinDias = 14; // Default 14 días
            var parametroEsTeams = await _smartDbContext.Set<Parametro>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Nombre == "EsTeams", cancellationToken);

            if (parametroEsTeams != null && int.TryParse(parametroEsTeams.Valor, out int parsedVal))
            {
                fechaFinDias = parsedVal;
            }

            // Finished sections (FechaFin + @FechaFinDias days in past)
            var finishedSectionIds = existingSections
                .Where(s => s.FechaFin.HasValue && s.FechaFin.Value.Date.AddDays(fechaFinDias) < DateTime.UtcNow.Date)
                .Select(s => s.IdSeccion)
                .ToHashSet();

            // Inactive pilot sections (EsActivo = false in dbo.TeamsSeccionesPiloto)
            var inactivePilotSectionIds = (await _smartDbContext.TeamsSeccionesPiloto
                .AsNoTracking()
                .Where(p => !p.EsActivo)
                .Select(p => p.IdSeccion)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var obsoleteTeams = activeTeams
                .Where(t => !existingSectionIds.Contains(t.IdSeccionSmart) 
                         || finishedSectionIds.Contains(t.IdSeccionSmart)
                         || inactivePilotSectionIds.Contains(t.IdSeccionSmart))
                .ToList();

            var processedList = new List<ObsoleteTeamProcessedDto>();
            var appClient = await _graphFactory.CreateClientAsync();

            foreach (var team in obsoleteTeams)
            {
                bool graphDeleted = false;
                string? graphError = null;

                if (!string.IsNullOrWhiteSpace(team.IdTeamsGroup))
                {
                    try
                    {
                        await appClient.Groups[team.IdTeamsGroup].DeleteAsync(cancellationToken: cancellationToken);
                        graphDeleted = true;
                        _logger.LogInformation("Deleted Microsoft Teams Group {GroupId} for obsolete section {IdSeccionSmart}.", team.IdTeamsGroup, team.IdSeccionSmart);
                    }
                    catch (Exception ex)
                    {
                        graphError = ex.Message;
                        _logger.LogWarning(ex, "Could not delete Group {GroupId} from Teams (may already be deleted or not found). Continuing with database soft-delete.", team.IdTeamsGroup);
                    }
                }

                // Soft-delete TeamsEquipos
                var dbTeams = await _smartDbContext.TeamsEquipos
                    .Where(t => t.IdTeamsGroup == team.IdTeamsGroup || t.IdSeccionSmart == team.IdSeccionSmart)
                    .ToListAsync(cancellationToken);

                foreach (var dbTeam in dbTeams)
                {
                    dbTeam.EstadoTeam = "I";
                    dbTeam.IsActive = "I";
                    dbTeam.FechaModificacion = DateTime.UtcNow;
                }

                // Soft-delete TeamsUsuarios
                if (!string.IsNullOrWhiteSpace(team.IdTeamsGroup))
                {
                    var members = await _smartDbContext.TeamsUsuarios
                        .Where(m => m.IdTeams == team.IdTeamsGroup && m.Estado == "A")
                        .ToListAsync(cancellationToken);

                    foreach (var member in members)
                    {
                        member.Estado = "I";
                        member.FechaModificacion = DateTime.UtcNow;
                    }

                    // Soft-delete TeamsHorarios
                    var sessions = await _smartDbContext.TeamsHorarios
                        .Where(s => s.IdTeams == team.IdTeamsGroup && s.Estado == "A")
                        .ToListAsync(cancellationToken);

                    foreach (var session in sessions)
                    {
                        session.Estado = "I";
                        session.FechaModificacion = DateTime.UtcNow;
                    }
                }

                await _smartDbContext.SaveChangesAsync(cancellationToken);

                string reason = inactivePilotSectionIds.Contains(team.IdSeccionSmart)
                    ? "desactivada en piloto (EsActivo=0)"
                    : finishedSectionIds.Contains(team.IdSeccionSmart)
                        ? "finalizada en Smart (FechaFin vencida)"
                        : "eliminada de Smart";
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Info",
                    EntidadAfectada = "Team",
                    Referencia = team.IdSeccionSmart.ToString(),
                    Mensaje = $"Sección {team.IdSeccionSmart} {reason}. Equipo {team.IdTeamsGroup} desactivado en TeamsEquipos (EstadoTeam='I') y borrado en Teams.",
                    Fecha = DateTime.UtcNow,
                    Severidad = "Low"
                });

                processedList.Add(new ObsoleteTeamProcessedDto
                {
                    IdSeccionSmart = team.IdSeccionSmart,
                    IdTeamsGroup = team.IdTeamsGroup,
                    NombreTeam = team.NombreTeam,
                    GraphDeleted = graphDeleted,
                    GraphError = graphError
                });
            }

            return new SyncObsoleteTeamsResult
            {
                TotalObsoleteFound = obsoleteTeams.Count,
                ProcessedTeams = processedList
            };
        }
    }
}

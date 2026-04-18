using APITeamsV3.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using APITeamsV3.Domain.Entities;
using System;
using System.Linq;
using APITeamsV3.Application.Common.Graph;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionTeamCommandHandler : IRequestHandler<SyncSectionTeamCommand, SyncSectionTeamResult>
    {
        private readonly ISmartDbContext _context;
        private readonly ITeamProvisioningService _provisioningService;
        private readonly ITeamAcademicoRepository _teamRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<SyncSectionTeamCommandHandler> _logger;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly IGraphClientFactory _graphClientFactory;

        public SyncSectionTeamCommandHandler(
            ISmartDbContext context,
            ITeamProvisioningService provisioningService,
            ITeamAcademicoRepository teamRepository,
            IMediator mediator,
            ILogger<SyncSectionTeamCommandHandler> logger,
            ITeamsLogOperativoRepository logRepository,
            IGraphClientFactory graphClientFactory)
        {
            _context = context;
            _provisioningService = provisioningService;
            _teamRepository = teamRepository;
            _mediator = mediator;
            _logger = logger;
            _logRepository = logRepository;
            _graphClientFactory = graphClientFactory;
        }

        public async Task<SyncSectionTeamResult> Handle(SyncSectionTeamCommand request, CancellationToken cancellationToken)
        {
            var result = new SyncSectionTeamResult();

            try
            {
                var existingTeam = await _context.TeamsEquipos
                    .FirstOrDefaultAsync(t => t.IdSeccionSmart == request.IdSeccion, cancellationToken);

                if (existingTeam == null)
                {
                    // Fetch the section data for provisioning
                    var section = await _context.Set<Seccion>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

                    if (section == null)
                    {
                        _logger.LogWarning($"Section {request.IdSeccion} not found in database. Aborting provision.");
                        result.Failure++;
                        return result;
                    }

                    // Flujo 1: Crear Team desde cero
                    _logger.LogInformation($"No existing team found for section {request.IdSeccion}. Provisioning new Microsoft 365 Group.");
                    
                    var newGraphId = await _provisioningService.ProvisionTeamAsync(section);
                    
                    if (string.IsNullOrEmpty(newGraphId)) 
                    {
                        await LogOperativoAsync(
                            "Error", 
                            "Team", 
                            request.IdSeccion.ToString(), 
                            "Error: Falló el aprovisionamiento en Microsoft Graph. Acción: Verifique que el administrador de la empresa tenga permisos suficientes y que el docente tenga una licencia válida de M365.", 
                            request.JobId);
                        result.Failure++;
                        return result;
                    }

                    // Keep creation flow aligned with RECREAR:
                    // owners are assigned during ProvisionTeamAsync; then we reconcile teachers/students deltas.
                    _logger.LogInformation($"Reconciling initial members and facilitators for new team {newGraphId}");
                    await _mediator.Send(new SyncTeamFacilitatorsCommand(request.IdSeccion, request.JobId), cancellationToken);
                    await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion, request.JobId), cancellationToken);
                    await _provisioningService.EnsureMembershipOpenAsync(newGraphId);
                    
                    await LogOperativoAsync("Success", "Team", newGraphId, "Equipo creado exitosamente con miembros y propietarios.", request.JobId);
                    result.Success++;
                }
                else
                {
                    // Flujo 4: Actualizar Team
                    _logger.LogInformation($"Updating Team for section {request.IdSeccion} (GroupId: {existingTeam.IdTeamsGroup})");

                    var graphClient = await _graphClientFactory.CreateClientAsync();
                    if (!await GraphGroupGuard.GroupExistsAsync(graphClient, existingTeam.IdTeamsGroup, cancellationToken))
                    {
                        existingTeam.EstadoTeam = "I";
                        existingTeam.FechaModificacion = DateTime.UtcNow;
                        await _teamRepository.UpdateAsync(existingTeam);

                        await LogOperativoAsync(
                            "Warning",
                            "Team",
                            existingTeam.IdTeamsGroup,
                            "Advertencia: El equipo vinculado ya no existe en Microsoft 365 (borrado externo). Acción: El sistema ha marcado el registro local como inactivo y creará un nuevo equipo automáticamente.",
                            request.JobId);

                        result.Ignored++;
                        return result;
                    }
                    
                    // Call the granular commands to synchronize changes
                    
                    // 1. Sync Nombres (Renamed)
                    await _mediator.Send(new SyncRenamedTeamsCommand(request.IdSeccion, request.JobId), cancellationToken);
                    
                    // 2. Sync Teachers
                    await _mediator.Send(new SyncTeamFacilitatorsCommand(request.IdSeccion, request.JobId), cancellationToken);
                    
                    // 3. Sync Students
                    await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion, request.JobId), cancellationToken);
                    
                    await LogOperativoAsync("Success", "Team", existingTeam.IdTeamsGroup, "Equipo actualizado exitosamente (Nombres, Owners, Miembros).", request.JobId);
                    result.Success++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error synchronizing section {request.IdSeccion}");
                await LogOperativoAsync("Error", "Seccion", request.IdSeccion.ToString(), ex.Message, request.JobId, ex.StackTrace ?? string.Empty);
                result.Failure++;
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
                _logger.LogWarning(ex, "Failed to write operational log in section sync.");
            }
        }
    }
}

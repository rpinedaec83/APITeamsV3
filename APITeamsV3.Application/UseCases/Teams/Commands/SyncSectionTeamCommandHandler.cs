using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using APITeamsV3.Domain.Entities;
using System;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionTeamCommandHandler : IRequestHandler<SyncSectionTeamCommand, SyncSectionTeamResult>
    {
        private readonly ISectionEligibilityService _eligibilityService;
        private readonly ITeamAcademicoRepository _teamRepository;
        private readonly ITeamProvisioningService _provisioningService;
        private readonly ISmartDbContext _context;
        private readonly ILogger<SyncSectionTeamCommandHandler> _logger;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly IMediator _mediator;

        public SyncSectionTeamCommandHandler(
            ISectionEligibilityService eligibilityService,
            ITeamAcademicoRepository teamRepository,
            ITeamProvisioningService provisioningService,
            ISmartDbContext context,
            ILogger<SyncSectionTeamCommandHandler> logger,
            ITeamsLogOperativoRepository logRepository,
            IMediator mediator)
        {
            _eligibilityService = eligibilityService;
            _teamRepository = teamRepository;
            _provisioningService = provisioningService;
            _context = context;
            _logger = logger;
            _logRepository = logRepository;
            _mediator = mediator;
        }

        public async Task<SyncSectionTeamResult> Handle(SyncSectionTeamCommand request, CancellationToken cancellationToken)
        {
            var result = new SyncSectionTeamResult();
            
            try
            {
                // 1. Refresh staging data for section essentially means load from local vw_MatriculasActivas or similar
                // Here we fetch the Seccion entity.
                var section = await _context.Set<Seccion>()
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);
                                            
                if (section == null)
                {
                    await LogOperativoAsync("Error", "Seccion", request.IdSeccion.ToString(), "La sección no existe en el repositorio local.", request.JobId);
                    result.Failure++;
                    return result;
                }

                // 2. Elegibilidad
                var eligibility = await _eligibilityService.IsEligibleForTeamsAsync(section, request.CompanyKey);
                if (!eligibility.IsEligible)
                {
                    await LogOperativoAsync("Info", "Seccion", request.IdSeccion.ToString(), $"Sección no elegible: {eligibility.Reason}", request.JobId);
                    result.Ignored++;
                    return result;
                }

                // 3. Revisar existencia local
                var existingTeam = await _teamRepository.GetBySeccionIdAsync(request.IdSeccion);

                if (existingTeam == null || existingTeam.EstadoTeam == "I")
                {
                    // 3.1 Ensure Snapshot Metadata exists (TeamsProgramacionGeneral)
                    var metadata = await _context.TeamsProgramacionGeneral
                        .AnyAsync(p => p.IdCurso == request.IdSeccion, cancellationToken);
                    
                    if (!metadata)
                    {
                        _logger.LogInformation($"Metadata missing for section {request.IdSeccion}. Forcing generation...");
                        await _mediator.Send(new GenerateSectionScheduleCommand(request.IdSeccion) { Force = true }, cancellationToken);
                    }

                    // Flujo 3: Crear Team
                    _logger.LogInformation($"Creating Team for section {request.IdSeccion}");
                    
                    // We delegate to the specific ITeamProvisioningService to handle Graph interactions and naming rules
                    // We prioritize the facilitator's email, then a default admin per company
                    // 3.1 Enforce teacher requirement — the section must have a facilitator
                    if (string.IsNullOrEmpty(section.EmailFacilitador))
                    {
                        string warnMsg = "No se puede crear el equipo: La sección no tiene un docente (Facilitador) asignado.";
                        _logger.LogWarning($"{warnMsg} Section ID: {request.IdSeccion}");
                        await LogOperativoAsync("Warning", "Seccion", request.IdSeccion.ToString(), warnMsg, request.JobId);
                        result.Ignored++;
                        return result;
                    }

                    var newGraphId = await _provisioningService.ProvisionTeamAsync(section);
                    
                    if (string.IsNullOrEmpty(newGraphId)) 
                    {
                        await LogOperativoAsync("Error", "Team", request.IdSeccion.ToString(), "Fallo aprovisionamiento en Graph", request.JobId);
                        result.Failure++;
                        return result;
                    }

                    // Keep creation flow aligned with RECREAR:
                    // owners are already assigned during ProvisionTeamAsync;
                    // here we only sync students as members.
                    _logger.LogInformation($"Populating initial members for new team {newGraphId}");
                    await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion), cancellationToken);
                    await _provisioningService.EnsureMembershipOpenAsync(newGraphId);
                    
                    await LogOperativoAsync("Success", "Team", newGraphId, "Equipo creado exitosamente con miembros y propietarios.", request.JobId);
                    result.Success++;
                }
                else
                {
                    // Flujo 4: Actualizar Team
                    _logger.LogInformation($"Updating Team for section {request.IdSeccion} (GroupId: {existingTeam.IdTeamsGroup})");
                    
                    // Call the granular commands to synchronize changes
                    
                    // 1. Sync Nombres (Renamed)
                    await _mediator.Send(new SyncRenamedTeamsCommand(request.IdSeccion), cancellationToken);
                    
                    // 2. Sync Facilitadores (Owners)
                    await _mediator.Send(new SyncTeamFacilitatorsCommand(request.IdSeccion), cancellationToken);
                    
                    // 3. Sync Estudiantes (Miembros)
                    await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion), cancellationToken);
                    await _mediator.Send(new SyncObsoleteStudentsCommand(request.IdSeccion), cancellationToken);
                    await _provisioningService.EnsureMembershipOpenAsync(existingTeam.IdTeamsGroup);

                    // If needed, evaluate Agenda (Regenerate)
                    // If team went from Active to Inactive -> SoftDeleteTeamCommand
                    
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
                _logger.LogWarning(ex, "Failed to write operational log. This might be due to a missing LogOperativo table.");
            }
        }
    }
}

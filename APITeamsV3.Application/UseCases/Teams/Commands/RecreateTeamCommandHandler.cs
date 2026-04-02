using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RecreateTeamCommandHandler : IRequestHandler<RecreateTeamCommand, RecreateTeamResult>
    {
        private readonly ITeamAcademicoRepository _teamRepository;
        private readonly IGraphClientFactory _graphFactory;
        private readonly ITeamProvisioningService _provisioningService;
        private readonly ISmartDbContext _context;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<RecreateTeamCommandHandler> _logger;
        private readonly IMediator _mediator;

        public RecreateTeamCommandHandler(
            ITeamAcademicoRepository teamRepository,
            IGraphClientFactory graphFactory,
            ITeamProvisioningService provisioningService,
            ISmartDbContext context,
            ITeamsLogOperativoRepository logRepository,
            ILogger<RecreateTeamCommandHandler> logger,
            IMediator mediator)
        {
            _teamRepository = teamRepository;
            _graphFactory = graphFactory;
            _provisioningService = provisioningService;
            _context = context;
            _logRepository = logRepository;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<RecreateTeamResult> Handle(RecreateTeamCommand request, CancellationToken cancellationToken)
        {
            var result = new RecreateTeamResult();

            try
            {
                // 1. Find the existing team record (active or inactive)
                var existingTeam = await _teamRepository.GetBySeccionIdAsync(request.IdSeccion);
                
                if (existingTeam != null && !string.IsNullOrEmpty(existingTeam.IdTeamsGroup))
                {
                    // 2. Delete the team from Microsoft Graph
                    try
                    {
                        var graphClient = await _graphFactory.CreateClientAsync();
                        await graphClient.Groups[existingTeam.IdTeamsGroup].DeleteAsync(cancellationToken: cancellationToken);
                        _logger.LogInformation($"Deleted Graph group {existingTeam.IdTeamsGroup} for section {request.IdSeccion}");
                        await LogOperativoAsync("Info", "TeamRecreate", existingTeam.IdTeamsGroup, 
                            "Equipo eliminado de Graph para recreación.");
                    }
                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
                    {
                        _logger.LogWarning($"Graph group {existingTeam.IdTeamsGroup} already deleted (404). Continuing with recreate.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error deleting Graph group {existingTeam.IdTeamsGroup}");
                        // Continue anyway — we still want to soft-delete locally and recreate
                    }

                    // 3. Soft-delete the old records in the database (TeamsEquipos, TeamsUsuarios, TeamsHorarios)
                    await _mediator.Send(new SoftDeleteTeamCommand(existingTeam.IdTeamsGroup), cancellationToken);
                    _logger.LogInformation($"Soft-deleted local records for {existingTeam.IdTeamsGroup}");
                    await LogOperativoAsync("Info", "TeamRecreate", existingTeam.IdTeamsGroup, 
                        "Registros locales marcados como inactivos (EstadoTeam=I).");
                }

                // 4. Fetch the section data
                var section = await _context.Set<Seccion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

                if (section == null)
                {
                    result.Success = false;
                    result.Summary = "La sección no existe en el repositorio local.";
                    return result;
                }

                // 5. Wait for Graph deletion to propagate
                await Task.Delay(5000, cancellationToken);

                // 6. Create new team in Graph (owners and metadata resolved internally by the service)
                var newGraphId = await _provisioningService.ProvisionTeamAsync(section);

                if (string.IsNullOrEmpty(newGraphId))
                {
                    result.Success = false;
                    result.Summary = "Falló la creación del nuevo equipo en Graph.";
                    await LogOperativoAsync("Error", "TeamRecreate", request.IdSeccion.ToString(), 
                        "Fallo aprovisionamiento en Graph durante recreación.");
                    return result;
                }

                // 8. Create new record in TeamsEquipos (via CreateTeamRecordCommand)
                await _mediator.Send(new CreateTeamRecordCommand 
                { 
                    IdSeccionSmart = request.IdSeccion, 
                    IdTeamsGroup = newGraphId 
                }, cancellationToken);

                // 9. Sync students into the new team
                await _mediator.Send(new SyncMissingStudentsCommand(request.IdSeccion), cancellationToken);

                await LogOperativoAsync("Success", "TeamRecreate", newGraphId, 
                    $"Equipo recreado exitosamente. Antiguo eliminado, nuevo creado con ID {newGraphId}.");

                result.Success = true;
                result.NewTeamId = newGraphId;
                result.Summary = $"Equipo recreado exitosamente. Nuevo ID: {newGraphId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recreating team for section {request.IdSeccion}");
                result.Success = false;
                result.Summary = $"Error al recrear equipo: {ex.Message}";
                await LogOperativoAsync("Error", "TeamRecreate", request.IdSeccion.ToString(), ex.Message);
            }

            return result;
        }

        private async Task LogOperativoAsync(string type, string target, string reference, string msg)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = type,
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    Fecha = DateTime.UtcNow,
                    Severidad = type == "Error" ? "High" : "Low"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write operational log.");
            }
        }
    }
}

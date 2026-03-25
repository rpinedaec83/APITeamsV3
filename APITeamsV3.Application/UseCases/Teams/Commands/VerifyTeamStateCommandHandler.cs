using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class VerifyTeamStateCommandHandler : IRequestHandler<VerifyTeamStateCommand, DiagnosticResultDto>
    {
        private readonly ITeamAcademicoRepository _teamRepository;
        private readonly IGraphClientFactory _graphFactory;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<VerifyTeamStateCommandHandler> _logger;

        public VerifyTeamStateCommandHandler(
            ITeamAcademicoRepository teamRepository,
            IGraphClientFactory graphFactory,
            ITeamsLogOperativoRepository logRepository,
            ILogger<VerifyTeamStateCommandHandler> logger)
        {
            _teamRepository = teamRepository;
            _graphFactory = graphFactory;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<DiagnosticResultDto> Handle(VerifyTeamStateCommand request, CancellationToken cancellationToken)
        {
            var result = new DiagnosticResultDto { IsValid = true, Summary = "Verificado." };

            var team = await _teamRepository.GetBySeccionIdAsync(request.IdSeccion);
            if (team == null)
            {
                result.IsValid = false;
                result.Summary = "No existe registro local del Team para la sección.";
                return result;
            }

            var graphClient = await _graphFactory.CreateClientAsync();

            try
            {
                var group = await graphClient.Groups[team.IdTeamsGroup].GetAsync(rc => 
                {
                    rc.QueryParameters.Select = new[] { "id", "displayName", "description" };
                }, cancellationToken);

                if (group != null)
                {
                    bool requiresUpdate = false;
                    
                    if (group.DisplayName != team.NombreTeam || group.Description != team.DescripcionTeam)
                    {
                        // Graph es la fuente de verdad (o Smart). Dependiendo de reglas, 
                        // si queremos alinear local con Graph o al revés:
                        team.NombreTeam = group.DisplayName ?? string.Empty;
                        team.DescripcionTeam = group.Description ?? string.Empty;
                        requiresUpdate = true;
                    }

                    if (requiresUpdate)
                    {
                        team.FechaModificacion = DateTime.UtcNow;
                        await _teamRepository.UpdateAsync(team);
                        await LogOperativoAsync("Info", "TeamMetadata", team.IdTeamsGroup, "Metadatos (Nombre/Descripción) autocorregidos.", request.JobId);
                        result.Summary += " Metadatos autocorregidos.";
                    }
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // El Team NO existe en Graph pero sí local.
                _logger.LogWarning($"Team {team.IdTeamsGroup} not found in Graph. Marking as inactive/deleted logically.");
                team.EstadoTeam = "I";
                team.IsActive = "I";
                team.FechaModificacion = DateTime.UtcNow;
                await _teamRepository.UpdateAsync(team);
                
                await LogOperativoAsync("Error", "TeamInconsistency", team.IdTeamsGroup, "El Team no existe en Graph. Removido lógicamente en BD.", request.JobId);
                
                result.IsValid = false;
                result.Summary = "Team inexistente en MS Graph. Borrado lógico aplicado.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying team {team.IdTeamsGroup}");
                result.IsValid = false;
                result.Summary = "Error técnico consultando MS Graph.";
                await LogOperativoAsync("Error", "TeamVerification", team.IdTeamsGroup, ex.Message, request.JobId);
            }

            return result;
        }

        private async Task LogOperativoAsync(string type, string target, string reference, string msg, string? jobId)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = type,
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    JobId = jobId,
                    Fecha = DateTime.UtcNow,
                    Severidad = type == "Error" ? "High" : "Low"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write operational log. This might be due to a missing LogOperativo table.");
            }
        }
    }
}

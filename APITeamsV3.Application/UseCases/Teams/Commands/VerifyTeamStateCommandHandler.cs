using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
            var result = new DiagnosticResultDto
            {
                IsValid = true,
                Summary = "Validacion completada."
            };

            var team = await _teamRepository.GetBySeccionIdAsync(request.IdSeccion);
            if (team == null)
            {
                result.IsValid = false;
                result.Summary = "No existe registro local del Team para la seccion.";
                return result;
            }

            var graphClient = await _graphFactory.CreateClientAsync();

            try
            {
                var group = await graphClient.Groups[team.IdTeamsGroup].GetAsync(rc =>
                {
                    rc.QueryParameters.Select = new[] { "id", "displayName", "description" };
                }, cancellationToken);

                if (group == null)
                {
                    result.IsValid = false;
                    result.TeamExistsInGraph = false;
                    result.Summary = "Se detecto una inconsistencia: no se obtuvo informacion del Team en Microsoft Graph.";
                    return result;
                }

                result.TeamExistsInGraph = true;
                var summaryMessages = new List<string>();
                var requiresUpdate = false;

                if (team.EstadoTeam == "I")
                {
                    team.EstadoTeam = "A";
                    team.IsActive = "A";
                    result.ReactivatedLocally = true;
                    requiresUpdate = true;
                    summaryMessages.Add("El Team existe en Microsoft Graph y estaba inactivo en la base local; se reactivo correctamente.");
                    await LogOperativoAsync("Info", "TeamState", team.IdTeamsGroup, "Team reactivado porque existe en Graph y estaba inactivo localmente.", request.JobId);
                }

                if (group.DisplayName != team.NombreTeam || group.Description != team.DescripcionTeam)
                {
                    team.NombreTeam = group.DisplayName ?? string.Empty;
                    team.DescripcionTeam = group.Description ?? string.Empty;
                    result.MetadataAutoCorrected = true;
                    requiresUpdate = true;
                    summaryMessages.Add("Se detectaron diferencias en nombre o descripcion y se autocorrigieron los metadatos locales.");
                }

                if (requiresUpdate)
                {
                    team.FechaModificacion = DateTime.UtcNow;
                    await _teamRepository.UpdateAsync(team);

                    if (result.MetadataAutoCorrected)
                    {
                        await LogOperativoAsync("Info", "TeamMetadata", team.IdTeamsGroup, "Metadatos (Nombre/Descripcion) autocorregidos.", request.JobId);
                    }
                }
                else
                {
                    summaryMessages.Add("El Team existe en Microsoft Graph y ya estaba sincronizado en la base local.");
                }

                result.Summary = string.Join(" ", summaryMessages);
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                team.EstadoTeam = "I";
                team.IsActive = "I";
                team.FechaModificacion = DateTime.UtcNow;
                await _teamRepository.UpdateAsync(team);

                result.IsValid = false;
                result.TeamExistsInGraph = false;
                result.MarkedInactiveLocally = true;
                result.Summary = "Se detecto una inconsistencia: el Team no existe en Microsoft Graph (404). Se marco como inactivo en la base local.";

                await LogOperativoAsync("Error", "TeamInconsistency", team.IdTeamsGroup, "El Team no existe en Graph (404). Marcado como inactivo en BD local.", request.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying team {TeamId}", team.IdTeamsGroup);
                result.IsValid = false;
                result.Summary = "Error tecnico consultando Microsoft Graph.";
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RegeneratePilotAgendasCommandHandler : IRequestHandler<RegeneratePilotAgendasCommand, RegeneratePilotAgendasResult>
    {
        private readonly ISmartDbContext _smartDbContext;
        private readonly IMediator _mediator;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<RegeneratePilotAgendasCommandHandler> _logger;

        public RegeneratePilotAgendasCommandHandler(
            ISmartDbContext smartDbContext,
            IMediator mediator,
            ITeamsLogOperativoRepository logRepository,
            ILogger<RegeneratePilotAgendasCommandHandler> logger)
        {
            _smartDbContext = smartDbContext;
            _mediator = mediator;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<RegeneratePilotAgendasResult> Handle(RegeneratePilotAgendasCommand request, CancellationToken cancellationToken)
        {
            var result = new RegeneratePilotAgendasResult();

            // Fetch all active sections in the pilot from TeamsEquipos
            var activeTeams = await _smartDbContext.TeamsEquipos
                .AsNoTracking()
                .Where(t => (t.EstadoTeam == "A" || t.IsActive == "A") && t.IdSeccionSmart > 0)
                .OrderBy(t => t.IdSeccionSmart)
                .ToListAsync(cancellationToken);

            if (activeTeams.Count == 0)
            {
                result.IsValid = true;
                result.Summary = "No se encontraron equipos de secciones activos en el piloto para regenerar agendas.";
                return result;
            }

            var sectionIds = activeTeams.Select(t => t.IdSeccionSmart).Distinct().ToList();
            result.TotalSections = sectionIds.Count;

            _logger.LogWarning(
                "NUCLEAR ACTION STARTED: Regenerating agendas for ALL {TotalSections} active pilot sections. Initiated by {ExecutedBy}.",
                result.TotalSections,
                request.ExecutedBy ?? "SYSTEM");

            await LogOperativoAsync(
                "Warning",
                "AgendaPilotNuclear",
                "NUCLEAR",
                $"Iniciando regeneración masiva de agendas para {result.TotalSections} secciones del piloto. Solicitado por: {request.ExecutedBy ?? "SYSTEM"}.",
                null,
                request.ExecutedBy);

            foreach (var sectionId in sectionIds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var sectionResult = await _mediator.Send(
                        new RegenerateAgendaCommand(sectionId, request.CompanyKey, null, request.ExecutedBy),
                        cancellationToken);

                    if (sectionResult.IsValid)
                    {
                        result.SucceededSections++;
                    }
                    else
                    {
                        result.FailedSections++;
                        result.FailedSectionCodes.Add($"Sección ID {sectionId}: {sectionResult.Summary}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to regenerate agenda for section {SectionId} during pilot bulk action.", sectionId);
                    result.FailedSections++;
                    result.FailedSectionCodes.Add($"Sección ID {sectionId}: {ex.Message}");
                }
            }

            result.IsValid = result.FailedSections == 0;
            result.Summary = $"Regeneración nuclear de piloto completada: {result.SucceededSections}/{result.TotalSections} secciones exitosas, {result.FailedSections} fallidas.";

            await LogOperativoAsync(
                result.IsValid ? "Success" : "Error",
                "AgendaPilotNuclear",
                "NUCLEAR",
                result.Summary,
                null,
                request.ExecutedBy);

            return result;
        }

        private async Task LogOperativoAsync(
            string type,
            string target,
            string reference,
            string msg,
            string? jobId,
            string? executedBy = null,
            string context = "")
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
                _logger.LogWarning(ex, "Error writing operational log for pilot agenda regeneration.");
            }
        }
    }
}

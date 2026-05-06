using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Models;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Recordings.Commands
{
    public class TransferRecordingsCommandHandler : IRequestHandler<TransferRecordingsCommand, RecordingTransferResult>
    {
        private readonly ITeamsRecordingTransferService _recordingsService;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<TransferRecordingsCommandHandler> _logger;

        public TransferRecordingsCommandHandler(
            ITeamsRecordingTransferService recordingsService,
            ITeamsLogOperativoRepository logRepository,
            ILogger<TransferRecordingsCommandHandler> logger)
        {
            _recordingsService = recordingsService;
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<RecordingTransferResult> Handle(TransferRecordingsCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _recordingsService.TransferAsync(request, cancellationToken);

                await TryLogAsync(request, result);
                return result;
            }
            catch (Exception ex)
            {
                await TryLogFailureAsync(request, ex);
                throw;
            }
        }

        private async Task TryLogAsync(TransferRecordingsCommand request, RecordingTransferResult result)
        {
            var hasEffectiveStorageExecution = result.FilesCopied > 0;
            var hasTransferErrors = result.FilesErrored > 0;

            // We will always log the attempt to provide visibility in the dashboard, 
            // even if 0 files were found or processed.

            try
            {
                var logType = hasTransferErrors
                    ? (hasEffectiveStorageExecution ? "Warning" : "Error")
                    : "Success";
                
                var reference = !string.IsNullOrWhiteSpace(request.SectionCode) 
                    ? request.SectionCode 
                    : (!string.IsNullOrWhiteSpace(result.TeamGroupId) ? result.TeamGroupId : "N/A");

                string message;
                if (hasEffectiveStorageExecution)
                {
                    message = $"Grabaciones almacenadas en Team: Encontrados={result.FilesFound}, Copiados={result.FilesCopied}, Omitidos={result.FilesSkipped}, Errores={result.FilesErrored}.";
                }
                else if (hasTransferErrors)
                {
                    message = $"Transferencia fallida o incompleta: Encontrados={result.FilesFound}, Errores={result.FilesErrored}. Revise el contexto tecnico.";
                }
                else
                {
                    var warningSummary = result.Warnings.Count > 0 ? $" | Aviso: {string.Join(" ", result.Warnings)}" : "";
                    message = $"No se procesaron grabaciones (0 encontradas o elegibles).{warningSummary}";
                }

                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = logType,
                    EntidadAfectada = "RecordingTransfer",
                    Referencia = reference,
                    Mensaje = message,
                    ContextoTecnico = string.Join(" | ", result.Errors),
                    Severidad = hasTransferErrors ? "Medium" : "Low",
                    JobId = request.JobId,
                    Usuario = request.ExecutedBy,
                    Fecha = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write RecordingTransfer operational log.");
            }
        }

        private async Task TryLogFailureAsync(TransferRecordingsCommand request, Exception ex)
        {
            try
            {
                var reference = !string.IsNullOrWhiteSpace(request.SectionCode)
                    ? request.SectionCode
                    : (!string.IsNullOrWhiteSpace(request.TeamGroupId) ? request.TeamGroupId : "N/A");

                var sectionLabel = request.SectionCode ?? request.SectionId?.ToString() ?? request.Section ?? "N/A";

                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Error",
                    EntidadAfectada = "RecordingTransfer",
                    Referencia = reference,
                    Mensaje = $"Transferencia de grabaciones fallida para la seccion {sectionLabel}: {ex.Message}",
                    ContextoTecnico = ex.ToString(),
                    Severidad = "High",
                    JobId = request.JobId,
                    Usuario = request.ExecutedBy,
                    Fecha = DateTime.UtcNow
                });
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to write failed RecordingTransfer operational log.");
            }
        }
    }
}

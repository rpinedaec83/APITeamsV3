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

                await TryLogAsync(result, request.JobId);
                return result;
            }
            catch (Exception ex)
            {
                await TryLogFailureAsync(request, ex);
                throw;
            }
        }

        private async Task TryLogAsync(RecordingTransferResult result, string? jobId)
        {
            var hasEffectiveStorageExecution = result.FilesCopied > 0;
            var hasTransferErrors = result.FilesErrored > 0;

            if (!hasEffectiveStorageExecution && !hasTransferErrors)
            {
                _logger.LogInformation(
                    "RecordingTransfer log omitted because no recordings were stored. Found={FilesFound}, Copied={FilesCopied}, Skipped={FilesSkipped}.",
                    result.FilesFound,
                    result.FilesCopied,
                    result.FilesSkipped);
                return;
            }

            try
            {
                var logType = hasTransferErrors
                    ? (hasEffectiveStorageExecution ? "Warning" : "Error")
                    : "Success";
                var reference = !string.IsNullOrWhiteSpace(result.TeamGroupId) ? result.TeamGroupId : "N/A";
                var message = hasEffectiveStorageExecution
                    ? $"Grabaciones almacenadas en Team: Encontrados={result.FilesFound}, Copiados={result.FilesCopied}, Omitidos={result.FilesSkipped}, Errores={result.FilesErrored}, EliminadosOrigen={result.SourceFilesDeleted}, ErrorEliminacionOrigen={result.SourceFilesDeleteErrors}."
                    : $"Transferencia de grabaciones ejecutada con errores y sin archivos almacenados: Encontrados={result.FilesFound}, Copiados={result.FilesCopied}, Omitidos={result.FilesSkipped}, Errores={result.FilesErrored}, EliminadosOrigen={result.SourceFilesDeleted}, ErrorEliminacionOrigen={result.SourceFilesDeleteErrors}.";

                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = logType,
                    EntidadAfectada = "RecordingTransfer",
                    Referencia = reference,
                    Mensaje = message,
                    ContextoTecnico = string.Join(" | ", result.Errors),
                    Severidad = hasTransferErrors ? "Medium" : "Low",
                    JobId = jobId,
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
                var reference = !string.IsNullOrWhiteSpace(request.TeamGroupId)
                    ? request.TeamGroupId
                    : request.SectionId?.ToString() ?? "N/A";

                var sectionLabel = request.SectionId?.ToString() ?? request.SectionCode ?? request.Section ?? "N/A";

                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Error",
                    EntidadAfectada = "RecordingTransfer",
                    Referencia = reference,
                    Mensaje = $"Transferencia de grabaciones fallida para la seccion {sectionLabel}: {ex.Message}",
                    ContextoTecnico = ex.ToString(),
                    Severidad = "High",
                    JobId = request.JobId,
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

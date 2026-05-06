using Hangfire;
using Hangfire.Server;
using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using APITeamsV3.Application.UseCases.Recordings.Commands;
using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using System;

namespace APITeamsV3.Infrastructure.Services
{
    public class HangfireJobService : IHangfireJobService
    {
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly CentralDbContext _centralDb;
        private readonly ISmartDbContext _smartDb;
        private readonly IEncryptionService _encryptionService;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;
        private readonly ILogger<HangfireJobService> _logger;

        public HangfireJobService(
            IMediator mediator,
            ITenantProvider tenantProvider,
            CentralDbContext centralDb,
            ISmartDbContext smartDb,
            IEncryptionService encryptionService,
            TenantHangfireRuntime tenantHangfireRuntime,
            ILogger<HangfireJobService> logger)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDb = centralDb;
            _smartDb = smartDb;
            _encryptionService = encryptionService;
            _tenantHangfireRuntime = tenantHangfireRuntime;
            _logger = logger;
        }

        private string GetCurrentCompanyKey()
        {
            try { return _tenantProvider.GetCurrentTenant().CompanyKey; }
            catch { return "idat"; } // Fallback for dev
        }

        /// <summary>
        /// Resolves and sets the tenant context for a Hangfire background job scope.
        /// Must be called at the start of every Send* method.
        /// </summary>
        private async Task<APITeamsV3.Domain.Entities.CompanyConfig> ResolveTenantAsync(string companyKey, bool includePilotSections = false)
        {
            var query = _centralDb.CompanyConfigs.AsQueryable();
            if (includePilotSections)
            {
                query = query.Include(c => c.PilotSections);
            }

            var config = await query
                .FirstOrDefaultAsync(c => c.CompanyKey == companyKey && c.IsActive);

            if (config == null)
                throw new InvalidOperationException($"Tenant '{companyKey}' not found or inactive.");

            _tenantProvider.SetTenant(new TenantContext
            {
                CompanyKey = config.CompanyKey,
                ConnectionString = _encryptionService.Decrypt(config.SmartConnectionString),
                TimeZoneId = config.TimeZoneId,
                GraphTenantId = config.GraphTenantId,
                GraphClientId = config.GraphClientId,
                GraphClientSecret = config.GraphClientSecretRef
            });

            return config;
        }

        private async Task<bool> ShouldRunForSectionAsync(string companyKey, int idSeccion, string jobName)
        {
            var config = await ResolveTenantAsync(companyKey, includePilotSections: true);

            if (!config.IsPilotMode)
            {
                return true;
            }

            var allowed = config.PilotSections.Any(ps => ps.IdSeccion == idSeccion);
            if (allowed)
            {
                return true;
            }

            _logger.LogWarning(
                "Hangfire job {JobName} omitido para seccion {IdSeccion} en tenant {CompanyKey}: fuera de la lista piloto.",
                jobName,
                idSeccion,
                companyKey);

            return false;
        }

        // Capture tenant key at request time, but enqueue against the tenant's own Hangfire storage.
        public async Task<string> EnqueueGenerateSchedule(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendGenerateSchedule(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueSyncDates(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncDates(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueSyncFacilitator(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncFacilitator(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueSyncRoster(int idSeccion, bool fullSync, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncRoster(idSeccion, fullSync, key, executedBy, null));
        }

        public async Task<string> EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendUpdateJoinUrl(idSeccion, joinUrl, idEvento, key, executedBy));
        }

        public async Task<string> EnqueueSyncMissingStudents(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncMissingStudents(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueSyncObsoleteStudents(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncObsoleteStudents(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueSyncRenamedTeams(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncRenamedTeams(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueFullSectionSync(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncRoster(idSeccion, true, key, executedBy, null));
        }

        public async Task<string> EnqueueSyncSectionTeam(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncSectionTeam(idSeccion, key, executedBy, null));
        }

        public async Task<string> EnqueuePilotRecordingTransfers(string companyKey, string? executedBy = null)
        {
            var client = await CreateClientAsync(companyKey);
            return client.Enqueue(() => RunPilotRecordingTransfers(companyKey, executedBy, null));
        }

        private async Task<IBackgroundJobClient> CreateClientAsync(string companyKey)
        {
            var storage = await _tenantHangfireRuntime.GetStorageAsync(companyKey);
            return new BackgroundJobClient(storage);
        }

        [JobDisplayName("Generate Schedule: Section {0} [{1}]")]
        public async Task SendGenerateSchedule(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "GenerateSchedule")) return;
            await _mediator.Send(new GenerateSectionScheduleCommand(idSeccion));
        }

        [JobDisplayName("Sync Dates: Section {0} [{1}]")]
        public async Task SendSyncDates(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncDates")) return;
            await _mediator.Send(new SyncSessionDatesCommand(idSeccion));
        }

        [JobDisplayName("Sync Facilitator: Section {0} [{1}]")]
        public async Task SendSyncFacilitator(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncFacilitator")) return;
            await _mediator.Send(new SyncSessionFacilitatorCommand(idSeccion));
        }

    public Task SendSyncRoster(int idSeccion, bool fullSync, string companyKey, string? executedBy = null)
        => SendSyncRoster(idSeccion, fullSync, companyKey, executedBy, null);
    
    public async Task SendSyncRoster(int idSeccion, bool fullSync, string companyKey, string? executedBy, PerformContext? performContext)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncRoster")) return;

            var jobId = performContext?.BackgroundJob?.Id;

            if (fullSync)
            {
                await _mediator.Send(new SyncSectionTeamCommand(idSeccion, companyKey, jobId, executedBy));
                await _mediator.Send(new SyncSectionAgendaCommand(idSeccion, companyKey, jobId, executedBy));
                return;
            }

            var command = new SyncSessionRosterCommand
            {
                IdSeccion = idSeccion,
                Mode = fullSync ? SessionRosterSyncType.FullSync : SessionRosterSyncType.EventSync
            };
            await _mediator.Send(command);
        }

        [JobDisplayName("Update Join URL: Section {0} [{3}]")]
        public async Task SendUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "UpdateJoinUrl")) return;
            await _mediator.Send(new UpdateSectionJoinUrlCommand(idSeccion, joinUrl, idEvento));
        }

        [JobDisplayName("Sync Missing Students: Section {0} [{1}]")]
        public async Task SendSyncMissingStudents(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncMissingStudents")) return;
            await _mediator.Send(new SyncMissingStudentsCommand(idSeccion));
        }

        [JobDisplayName("Sync Obsolete Students: Section {0} [{1}]")]
        public async Task SendSyncObsoleteStudents(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncObsoleteStudents")) return;
            await _mediator.Send(new SyncObsoleteStudentsCommand(idSeccion));
        }

        [JobDisplayName("Sync Renamed Teams: Section {0} [{1}]")]
        public async Task SendSyncRenamedTeams(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncRenamedTeams")) return;
            await _mediator.Send(new SyncRenamedTeamsCommand(idSeccion));
        }

        public Task SendSyncSectionTeam(int idSeccion, string companyKey, string? executedBy = null)
            => SendSyncSectionTeam(idSeccion, companyKey, executedBy, null);

        [JobDisplayName("Sync Section Team V3: Section {0} [{1}]")]
        public async Task SendSyncSectionTeam(int idSeccion, string companyKey, string? executedBy, PerformContext? performContext)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncSectionTeam")) return;
            var jobId = performContext?.BackgroundJob?.Id;
            await _mediator.Send(new SyncSectionTeamCommand(idSeccion, companyKey, jobId, executedBy));
        }

        [JobDisplayName("Regenerate Agenda: Section {0} [{1}]")]
        public async Task SendRegenerateAgenda(int idSeccion, string companyKey, string? jobId, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "RegenerateAgenda")) return;

            var result = await _mediator.Send(new RegenerateAgendaCommand(idSeccion, companyKey, jobId, executedBy));
            if (!result.IsValid)
            {
                _logger.LogWarning(
                    "Agenda regeneration for section {IdSeccion} in tenant {CompanyKey} finished as invalid: {Summary}",
                    idSeccion,
                    companyKey,
                    result.Summary);

                if (result.Summary.Contains("aprovisionando", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(result.Summary);
                }
            }
        }

        public Task RunPilotRecordingTransfers(string companyKey, string? executedBy = null)
            => RunPilotRecordingTransfers(companyKey, executedBy, null);

        [JobDisplayName("Transfer Pilot Recordings [{0}]")]
        public async Task RunPilotRecordingTransfers(string companyKey, string? executedBy, PerformContext? performContext)
        {
            var hangfireJobId = performContext?.BackgroundJob?.Id;
            var config = await ResolveTenantAsync(companyKey, includePilotSections: true);
            if (!config.IsRecordingTransferJobEnabled)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: IsRecordingTransferJobEnabled is disabled.",
                    companyKey);
                return;
            }

            if (!config.IsPilotMode)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: IsPilotMode is disabled.",
                    companyKey);
                return;
            }

            var pilotSectionIds = config.PilotSections
                .Select(ps => ps.IdSeccion)
                .Distinct()
                .ToList();

            if (pilotSectionIds.Count == 0)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: there are no pilot sections configured.",
                    companyKey);
                return;
            }

            var failures = new List<string>();
            var processedSections = 0;
            var skippedInconsistentSections = 0;

            foreach (var idSeccion in pilotSectionIds)
            {
                try
                {
                    var anyTeam = await _smartDb.Set<TeamEntity>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            t => t.IdSeccionSmart == idSeccion,
                            CancellationToken.None);

                    var team = await _smartDb.Set<TeamEntity>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            t => t.IdSeccionSmart == idSeccion && t.EstadoTeam == "A" && t.IsActive == "A",
                            CancellationToken.None);

                    if (team == null || string.IsNullOrWhiteSpace(team.IdTeamsGroup))
                    {
                        if (anyTeam != null && (!string.Equals(anyTeam.EstadoTeam, "A", StringComparison.OrdinalIgnoreCase) ||
                                                !string.Equals(anyTeam.IsActive, "A", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogInformation(
                                "Recording transfer skipped for tenant {CompanyKey}, section {IdSeccion}: team is inactive (EstadoTeam={EstadoTeam}, IsActive={IsActive}).",
                                companyKey,
                                idSeccion,
                                anyTeam.EstadoTeam,
                                anyTeam.IsActive);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Recording transfer skipped for tenant {CompanyKey}, section {IdSeccion}: no active team found.",
                                companyKey,
                                idSeccion);
                        }

                        continue;
                    }

                    var section = await _smartDb.Set<Seccion>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.IdSeccion == idSeccion, CancellationToken.None);

                    var result = await _mediator.Send(new TransferRecordingsCommand
                    {
                        JobId = hangfireJobId,
                        ExecutedBy = executedBy,
                        OrganizerUserPrincipalName = string.IsNullOrWhiteSpace(team.Propietario2) ? null : team.Propietario2,
                        TeamGroupId = team.IdTeamsGroup,
                        CourseName = section?.CursoNombre,
                        Section = !string.IsNullOrWhiteSpace(section?.GrupoCodigo) ? section.GrupoCodigo : section?.Codigo,
                        SectionId = idSeccion,
                        SectionCode = !string.IsNullOrWhiteSpace(section?.Codigo) ? section.Codigo : section?.GrupoCodigo
                    });

                    processedSections++;

                    if (result.FilesErrored > 0)
                    {
                        var errorSummary = result.Errors.Count > 0
                            ? string.Join(" | ", result.Errors.Take(3))
                            : $"Se registraron {result.FilesErrored} error(es) durante la transferencia.";

                        failures.Add($"Seccion {idSeccion}: {errorSummary}");
                    }
                }
                catch (Exception ex)
                {
                    if (IsRecoverableRecordingTransferInconsistency(ex))
                    {
                        skippedInconsistentSections++;
                        _logger.LogWarning(
                            ex,
                            "Recording transfer skipped as inconsistent for tenant {CompanyKey}, section {IdSeccion}.",
                            companyKey,
                            idSeccion);
                        continue;
                    }

                    _logger.LogError(
                        ex,
                        "Recording transfer failed for tenant {CompanyKey}, section {IdSeccion}.",
                        companyKey,
                        idSeccion);

                    failures.Add($"Seccion {idSeccion}: {ex.Message}");
                }
            }

            if (failures.Count > 0)
            {
                var summary = string.Join(" || ", failures.Take(5));
                throw new InvalidOperationException(
                    $"Recording transfer completed with failures for tenant {companyKey}. " +
                    $"Procesadas={processedSections}, Fallidas={failures.Count}. Detalle: {summary}");
            }

            if (skippedInconsistentSections > 0)
            {
                _logger.LogWarning(
                    "Recording transfer completed for tenant {CompanyKey} with {SkippedInconsistentSections} inconsistent section(s) skipped. Procesadas={ProcessedSections}.",
                    companyKey,
                    skippedInconsistentSections,
                    processedSections);
            }
        }

        private static bool IsRecoverableRecordingTransferInconsistency(Exception ex)
        {
            if (ex is ODataError odataError && IsGraphNotFoundStatus(odataError.ResponseStatusCode))
            {
                return MessageMatchesRecoverableRecordingTransferInconsistency(odataError.Message);
            }

            if (ex is ApiException apiException && IsGraphNotFoundStatus(apiException.ResponseStatusCode))
            {
                return MessageMatchesRecoverableRecordingTransferInconsistency(apiException.Message);
            }

            return MessageMatchesRecoverableRecordingTransferInconsistency(ex.Message);
        }

        private static bool IsGraphNotFoundStatus(int statusCode)
        {
            return statusCode == 404;
        }

        private static bool MessageMatchesRecoverableRecordingTransferInconsistency(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("GetChildThreadsV2Async", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("GetThreadS2SRequest", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Requested API is not supported", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Resource is not found", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("No se encontro el canal", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("No se pudo resolver filesFolder", StringComparison.OrdinalIgnoreCase);
        }
    }
}

using Hangfire;
using Hangfire.Server;
using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using APITeamsV3.Application.UseCases.Recordings.Commands;
using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Options;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly ITeamsRecordingTransferService _recordingTransferService;
        private readonly RecordingTransferOptions _recordingTransferOptions;

        public HangfireJobService(
            IMediator mediator,
            ITenantProvider tenantProvider,
            CentralDbContext centralDb,
            ISmartDbContext smartDb,
            IEncryptionService encryptionService,
            TenantHangfireRuntime tenantHangfireRuntime,
            ILogger<HangfireJobService> logger,
            ITeamsRecordingTransferService recordingTransferService,
            IOptions<RecordingTransferOptions> recordingTransferOptions)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDb = centralDb;
            _smartDb = smartDb;
            _encryptionService = encryptionService;
            _tenantHangfireRuntime = tenantHangfireRuntime;
            _logger = logger;
            _recordingTransferService = recordingTransferService;
            _recordingTransferOptions = recordingTransferOptions.Value;
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
                CompanyId = config.Id,
                CompanyKey = config.CompanyKey,
                DisplayName = config.DisplayName ?? string.Empty,
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

            var allowedInSmart = await _smartDb.TeamsSeccionesPiloto
                .AsNoTracking()
                .AnyAsync(ps => ps.IdSeccion == idSeccion && ps.EsActivo);

            if (allowedInSmart)
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

        public async Task<string> EnqueueSyncAttendance(int idSeccion, string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncAttendance(idSeccion, key, executedBy));
        }

        public async Task<string> EnqueueCheckStorageQuota(string? executedBy = null)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendCheckStorageQuota(key, executedBy));
        }

        public async Task<string> EnqueuePilotRecordingTransfers(string companyKey, string? executedBy = null)
        {
            var client = await CreateClientAsync(companyKey);
            return client.Enqueue(() => RunPilotRecordingTransfers(companyKey, executedBy, null));
        }

        public async Task<string> EnqueueAllRecordingTransfers(string companyKey, string? executedBy = null)
        {
            var client = await CreateClientAsync(companyKey);
            return client.Enqueue(() => RunAllRecordingTransfers(companyKey, executedBy, null));
        }

        public async Task<string> EnqueueRecordingTransferForSection(int idSeccion, string companyKey, string? executedBy = null)
        {
            var client = await CreateClientAsync(companyKey);
            return client.Enqueue(() => RunRecordingTransferForSection(idSeccion, companyKey, executedBy, null));
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
                await _mediator.Send(new SyncSectionAttendanceCommand(idSeccion, companyKey, jobId, executedBy));
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

        [JobDisplayName("Sync Attendance: Section {0} [{1}]")]
        public async Task SendSyncAttendance(int idSeccion, string companyKey, string? executedBy = null)
        {
            if (!await ShouldRunForSectionAsync(companyKey, idSeccion, "SyncAttendance")) return;
            await _mediator.Send(new SyncSectionAttendanceCommand(idSeccion, companyKey, null, executedBy));
        }

        [JobDisplayName("Sync SharePoint Quota & Alert Check [{1}]")]
        public async Task SendCheckStorageQuota(string companyKey, string? executedBy = null)
        {
            await ResolveTenantAsync(companyKey, includePilotSections: false);
            var result = await _recordingTransferService.GetStorageQuotaAsync(forceEmailAlert: true, CancellationToken.None);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Error en verificación de cuota de almacenamiento: {result.ErrorMessage}");
            }
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

        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
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

            var targetModalidadId = _recordingTransferOptions.RequiredModalidadId ?? 29690;
            var validPilotSectionIds = await _smartDb.SeccionTable
                .AsNoTracking()
                .Where(s => pilotSectionIds.Contains(s.IdSeccion) && s.IdTipoModalidad == targetModalidadId)
                .Select(s => s.IdSeccion)
                .Distinct()
                .ToListAsync(CancellationToken.None);

            if (validPilotSectionIds.Count == 0)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: none of the {TotalPilot} pilot sections match IdTipoModalidad={TargetModalidad}.",
                    companyKey,
                    pilotSectionIds.Count,
                    targetModalidadId);
                return;
            }

            if (validPilotSectionIds.Count < pilotSectionIds.Count)
            {
                var omittedCount = pilotSectionIds.Count - validPilotSectionIds.Count;
                _logger.LogInformation(
                    "Tenant {CompanyKey}: {OmittedCount} pilot section(s) omitted because they do not match IdTipoModalidad={TargetModalidad}.",
                    companyKey,
                    omittedCount,
                    targetModalidadId);
            }

            var failures = new List<string>();
            var state = new ProcessRecordingState();
            await ProcessRecordingTransfersForSections(
                companyKey,
                executedBy,
                hangfireJobId,
                validPilotSectionIds,
                failures,
                state);

            LogAndThrowTransferResults(companyKey, state.ProcessedSections, state.SkippedInconsistentSections, failures);
        }

        public Task RunAllRecordingTransfers(string companyKey, string? executedBy = null)
            => RunAllRecordingTransfers(companyKey, executedBy, null);

        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 7200)]
        [JobDisplayName("Transfer All Recordings [{0}]")]
        public async Task RunAllRecordingTransfers(string companyKey, string? executedBy, PerformContext? performContext)
        {
            var hangfireJobId = performContext?.BackgroundJob?.Id;
            var config = await ResolveTenantAsync(companyKey, includePilotSections: false);
            if (!config.IsRecordingTransferJobEnabled)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: IsRecordingTransferJobEnabled is disabled.",
                    companyKey);
                return;
            }

            if (config.IsPilotMode)
            {
                _logger.LogInformation(
                    "All-sections recording transfer job omitted for tenant {CompanyKey}: tenant is in Pilot Mode.",
                    companyKey);
                return;
            }

            var targetModalidadId = _recordingTransferOptions.RequiredModalidadId ?? 29690;

            // In non-pilot mode, process ALL active sections that have an active Team and match the required modality.
            var activeSectionIds = await (
                from t in _smartDb.Set<TeamEntity>().AsNoTracking()
                join s in _smartDb.SeccionTable.AsNoTracking() on t.IdSeccionSmart equals s.IdSeccion
                where t.EstadoTeam == "A" && t.IsActive == "A" && t.IdSeccionSmart > 0
                      && s.IdTipoModalidad == targetModalidadId
                select t.IdSeccionSmart
            ).Distinct().ToListAsync(CancellationToken.None);

            if (activeSectionIds.Count == 0)
            {
                _logger.LogInformation(
                    "Recording transfer job omitted for tenant {CompanyKey}: there are no active teams with IdTipoModalidad={TargetModalidad}.",
                    companyKey,
                    targetModalidadId);
                return;
            }

            _logger.LogInformation(
                "Tenant {CompanyKey} running recording transfer for active teams with IdTipoModalidad={TargetModalidad}. Total sections: {Count}.",
                companyKey, targetModalidadId, activeSectionIds.Count);

            var failures = new List<string>();
            var state = new ProcessRecordingState();
            await ProcessRecordingTransfersForSections(
                companyKey,
                executedBy,
                hangfireJobId,
                activeSectionIds,
                failures,
                state);

            LogAndThrowTransferResults(companyKey, state.ProcessedSections, state.SkippedInconsistentSections, failures);
        }

        [AutomaticRetry(Attempts = 0)]
        [JobDisplayName("Transfer Recordings for Section {0}")]
        public async Task RunRecordingTransferForSection(int idSeccion, string companyKey, string? executedBy, PerformContext? performContext)
        {
            var hangfireJobId = performContext?.BackgroundJob?.Id;
            var failures = new List<string>();
            var state = new ProcessRecordingState();

            _logger.LogInformation(
                "Tenant {CompanyKey} running manual recording transfer for section {IdSeccion} executed by {ExecutedBy}.",
                companyKey, idSeccion, executedBy ?? "unknown");

            // VERY IMPORTANT: Initialize the tenant context for this background thread
            await ResolveTenantAsync(companyKey, includePilotSections: false);

            await ProcessRecordingTransfersForSections(
                companyKey,
                executedBy,
                hangfireJobId,
                new List<int> { idSeccion },
                failures,
                state);

            LogAndThrowTransferResults(companyKey, state.ProcessedSections, state.SkippedInconsistentSections, failures);
        }

        private async Task ProcessRecordingTransfersForSections(
            string companyKey,
            string? executedBy,
            string? hangfireJobId,
            List<int> sectionIds,
            List<string> failures,
            ProcessRecordingState state)
        {
            var targetModalidadId = _recordingTransferOptions.RequiredModalidadId ?? 29690;

            foreach (var idSeccion in sectionIds)
            {
                try
                {
                    var sectionTable = await _smartDb.SeccionTable
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.IdSeccion == idSeccion, CancellationToken.None);

                    if (sectionTable == null || sectionTable.IdTipoModalidad != targetModalidadId)
                    {
                        var msg = $"IdTipoModalidad ({sectionTable?.IdTipoModalidad}) no cumple con la regla requerida ({targetModalidadId}).";
                        _logger.LogInformation(
                            "Recording transfer skipped for tenant {CompanyKey}, section {IdSeccion}: {Reason}",
                            companyKey,
                            idSeccion,
                            msg);

                        if (sectionIds.Count == 1)
                        {
                            failures.Add($"Seccion {idSeccion}: {msg}");
                        }

                        continue;
                    }
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

                    state.ProcessedSections++;

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
                        state.SkippedInconsistentSections++;
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
        }

        private void LogAndThrowTransferResults(string companyKey, int processedSections, int skippedInconsistentSections, List<string> failures)
        {
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
                   message.Contains("No se pudo resolver filesFolder", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("No existe carpeta origen", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("No se pudo resolver el Drive", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("tiempo límite de", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("configured HttpClient.Timeout", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Timeout esperando confirmacion de copia", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ProcessRecordingState
        {
            public int ProcessedSections { get; set; } = 0;
            public int SkippedInconsistentSections { get; set; } = 0;
        }
    }
}

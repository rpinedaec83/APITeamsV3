using Hangfire;
using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Infrastructure.Services
{
    public class HangfireJobService : IHangfireJobService
    {
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly CentralDbContext _centralDb;
        private readonly IEncryptionService _encryptionService;
        private readonly TenantHangfireRuntime _tenantHangfireRuntime;

        public HangfireJobService(
            IMediator mediator,
            ITenantProvider tenantProvider,
            CentralDbContext centralDb,
            IEncryptionService encryptionService,
            TenantHangfireRuntime tenantHangfireRuntime)
        {
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDb = centralDb;
            _encryptionService = encryptionService;
            _tenantHangfireRuntime = tenantHangfireRuntime;
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
        private async Task ResolveTenantAsync(string companyKey)
        {
            var config = await _centralDb.CompanyConfigs
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
        }

        // Capture tenant key at request time, but enqueue against the tenant's own Hangfire storage.
        public async Task<string> EnqueueGenerateSchedule(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendGenerateSchedule(idSeccion, key));
        }

        public async Task<string> EnqueueSyncDates(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncDates(idSeccion, key));
        }

        public async Task<string> EnqueueSyncFacilitator(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncFacilitator(idSeccion, key));
        }

        public async Task<string> EnqueueSyncRoster(int idSeccion, bool fullSync)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncRoster(idSeccion, fullSync, key));
        }

        public async Task<string> EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendUpdateJoinUrl(idSeccion, joinUrl, idEvento, key));
        }

        public async Task<string> EnqueueSyncMissingStudents(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncMissingStudents(idSeccion, key));
        }

        public async Task<string> EnqueueSyncObsoleteStudents(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncObsoleteStudents(idSeccion, key));
        }

        public async Task<string> EnqueueSyncRenamedTeams(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncRenamedTeams(idSeccion, key));
        }

        public async Task<string> EnqueueFullSectionSync(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);

            var j1 = client.Enqueue(() => SendSyncMissingStudents(idSeccion, key));
            var j2 = client.ContinueJobWith(j1, () => SendSyncObsoleteStudents(idSeccion, key));
            client.ContinueJobWith(j2, () => SendSyncRenamedTeams(idSeccion, key));

            return j1;
        }

        public async Task<string> EnqueueSyncSectionTeam(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            var client = await CreateClientAsync(key);
            return client.Enqueue(() => SendSyncSectionTeam(idSeccion, key, null));
        }

        private async Task<IBackgroundJobClient> CreateClientAsync(string companyKey)
        {
            var storage = await _tenantHangfireRuntime.GetStorageAsync(companyKey);
            return new BackgroundJobClient(storage);
        }

        [JobDisplayName("Generate Schedule: Section {0} [{1}]")]
        public async Task SendGenerateSchedule(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new GenerateSectionScheduleCommand(idSeccion));
        }

        [JobDisplayName("Sync Dates: Section {0} [{1}]")]
        public async Task SendSyncDates(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncSessionDatesCommand(idSeccion));
        }

        [JobDisplayName("Sync Facilitator: Section {0} [{1}]")]
        public async Task SendSyncFacilitator(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncSessionFacilitatorCommand(idSeccion));
        }

        [JobDisplayName("Sync Roster: Section {0} (Full: {1}) [{2}]")]
        public async Task SendSyncRoster(int idSeccion, bool fullSync, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            var command = new SyncSessionRosterCommand
            {
                IdSeccion = idSeccion,
                Mode = fullSync ? SessionRosterSyncType.FullSync : SessionRosterSyncType.EventSync
            };
            await _mediator.Send(command);
        }

        [JobDisplayName("Update Join URL: Section {0} [{3}]")]
        public async Task SendUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new UpdateSectionJoinUrlCommand(idSeccion, joinUrl, idEvento));
        }

        [JobDisplayName("Sync Missing Students: Section {0} [{1}]")]
        public async Task SendSyncMissingStudents(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncMissingStudentsCommand(idSeccion));
        }

        [JobDisplayName("Sync Obsolete Students: Section {0} [{1}]")]
        public async Task SendSyncObsoleteStudents(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncObsoleteStudentsCommand(idSeccion));
        }

        [JobDisplayName("Sync Renamed Teams: Section {0} [{1}]")]
        public async Task SendSyncRenamedTeams(int idSeccion, string companyKey)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncRenamedTeamsCommand(idSeccion));
        }

        [JobDisplayName("Sync Section Team V3: Section {0} [{1}]")]
        public async Task SendSyncSectionTeam(int idSeccion, string companyKey, string? jobId)
        {
            await ResolveTenantAsync(companyKey);
            await _mediator.Send(new SyncSectionTeamCommand(idSeccion, companyKey, jobId));
        }
    }
}

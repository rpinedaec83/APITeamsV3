using Hangfire;
using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Provisioning.Commands;
using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using System;

namespace APITeamsV3.Infrastructure.Services
{
    public class HangfireJobService : IHangfireJobService
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IMediator _mediator;
        private readonly ITenantProvider _tenantProvider;
        private readonly CentralDbContext _centralDb;
        private readonly IEncryptionService _encryptionService;

        public HangfireJobService(
            IBackgroundJobClient backgroundJobClient,
            IMediator mediator,
            ITenantProvider tenantProvider,
            CentralDbContext centralDb,
            IEncryptionService encryptionService)
        {
            _backgroundJobClient = backgroundJobClient;
            _mediator = mediator;
            _tenantProvider = tenantProvider;
            _centralDb = centralDb;
            _encryptionService = encryptionService;
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

        // ── Enqueue methods (capture tenant key at request time) ──

        public string EnqueueGenerateSchedule(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendGenerateSchedule(idSeccion, key));
        }

        public string EnqueueSyncDates(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncDates(idSeccion, key));
        }

        public string EnqueueSyncFacilitator(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncFacilitator(idSeccion, key));
        }

        public string EnqueueSyncRoster(int idSeccion, bool fullSync)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncRoster(idSeccion, fullSync, key));
        }

        public string EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendUpdateJoinUrl(idSeccion, joinUrl, idEvento, key));
        }

        public string EnqueueSyncMissingStudents(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncMissingStudents(idSeccion, key));
        }

        public string EnqueueSyncObsoleteStudents(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncObsoleteStudents(idSeccion, key));
        }

        public string EnqueueSyncRenamedTeams(int idSeccion)
        {
            var key = GetCurrentCompanyKey();
            return _backgroundJobClient.Enqueue(() => SendSyncRenamedTeams(idSeccion, key));
        }

        // ── Send methods (resolve tenant, then dispatch command) ──

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
    }
}

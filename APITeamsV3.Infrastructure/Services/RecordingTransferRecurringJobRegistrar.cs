using APITeamsV3.Infrastructure.Persistence.Contexts;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Infrastructure.Services
{
    public sealed class RecordingTransferRecurringJobRegistrar : BackgroundService
    {
        private const string RecurringJobId = "recordings-transfer-pilot";

        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecordingTransferRecurringJobRegistrar> _logger;
        private readonly TimeSpan _refreshInterval;

        public RecordingTransferRecurringJobRegistrar(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<RecordingTransferRecurringJobRegistrar> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;

            var refreshSeconds = _configuration.GetValue<int?>("Hangfire:RecurringRegistrationRefreshSeconds") ?? 300;
            _refreshInterval = TimeSpan.FromSeconds(Math.Max(60, refreshSeconds));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshRecurringJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh recurring recording transfer jobs.");
                }

                await Task.Delay(_refreshInterval, stoppingToken);
            }
        }

        private async Task RefreshRecurringJobsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var centralDb = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
            var runtime = scope.ServiceProvider.GetRequiredService<TenantHangfireRuntime>();

            var activeCompanies = await centralDb.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var company in activeCompanies)
            {
                try
                {
                    var storage = await runtime.GetStorageAsync(company.CompanyKey, cancellationToken);
                    var manager = new RecurringJobManager(storage);

                    if (!company.IsPilotMode || company.PilotSections.Count == 0 || !company.IsRecordingTransferJobEnabled)
                    {
                        manager.RemoveIfExists(RecurringJobId);
                        continue;
                    }

                    var cron = ResolveCron(company.RecordingTransferCron);
                    var timeZone = ResolveTimeZone(company.TimeZoneId);

                    manager.AddOrUpdate<HangfireJobService>(
                        RecurringJobId,
                        service => service.RunPilotRecordingTransfers(company.CompanyKey, null),
                        cron,
                        new RecurringJobOptions
                        {
                            TimeZone = timeZone
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to register recurring recording transfer job for tenant {CompanyKey}.",
                        company.CompanyKey);
                }
            }
        }

        private string ResolveCron(string? tenantCron)
        {
            if (!string.IsNullOrWhiteSpace(tenantCron))
            {
                return tenantCron.Trim();
            }

            var configuredCron = _configuration["Hangfire:RecordingTransferCron"];
            return string.IsNullOrWhiteSpace(configuredCron) ? Cron.Hourly() : configuredCron.Trim();
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch
                {
                }
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}

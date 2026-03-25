using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Sedes;
using APITeamsV3.Application.UseCases.Teams.Commands;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    /// <summary>
    /// Background service that checks sync schedules every minute
    /// and triggers full sync when a schedule matches the current day/time.
    /// </summary>
    public class SyncSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SyncSchedulerService> _logger;

        public SyncSchedulerService(IServiceProvider serviceProvider, ILogger<SyncSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SyncSchedulerService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndRunSchedules(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SyncSchedulerService loop.");
                }

                // Check every 60 seconds
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task CheckAndRunSchedules(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var centralDb = scope.ServiceProvider.GetRequiredService<ICentralDbContext>();

            var now = DateTime.Now; // Use local time
            var currentDay = now.DayOfWeek switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                _ => ""
            };

            var schedules = await centralDb.SyncSchedules
                .Where(s => s.IsEnabled && s.Hour == now.Hour && s.Minute == now.Minute)
                .ToListAsync(stoppingToken);

            foreach (var schedule in schedules)
            {
                // Check if today's day is in the schedule
                var days = schedule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!days.Contains(currentDay)) continue;

                // Avoid re-running if already ran this minute
                if (schedule.LastRunAt.HasValue &&
                    schedule.LastRunAt.Value.Date == now.Date &&
                    schedule.LastRunAt.Value.Hour == now.Hour &&
                    schedule.LastRunAt.Value.Minute == now.Minute)
                {
                    continue;
                }

                _logger.LogInformation("Schedule {Id} triggered for company {CompanyId} at {Time}",
                    schedule.Id, schedule.CompanyConfigId, now);

                try
                {
                    // Get active sede codes for this company
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var sedeCodes = await mediator.Send(new GetActiveSedeCodesQuery(schedule.CompanyConfigId, true), stoppingToken);

                    if (string.IsNullOrEmpty(sedeCodes))
                    {
                        _logger.LogWarning("Schedule {Id}: No active sedes for company {CompanyId}. Skipping.",
                            schedule.Id, schedule.CompanyConfigId);
                        continue;
                    }

                    // Resolve tenant and trigger full sync
                    var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                    var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
                    var companyConfig = await centralDb.CompanyConfigs
                        .FirstOrDefaultAsync(c => c.Id == schedule.CompanyConfigId, stoppingToken);

                    if (companyConfig == null) continue;

                    tenantProvider.SetTenant(new TenantContext
                    {
                        CompanyKey = companyConfig.CompanyKey,
                        ConnectionString = encryptionService.Decrypt(companyConfig.SmartConnectionString),
                        TimeZoneId = companyConfig.TimeZoneId,
                        GraphTenantId = companyConfig.GraphTenantId,
                        GraphClientId = companyConfig.GraphClientId,
                        GraphClientSecret = companyConfig.GraphClientSecretRef
                    });

                    // Trigger the sync
                    var result = await mediator.Send(new SyncAllTeamsCommand(sedeCodes), stoppingToken);

                    _logger.LogInformation("Schedule {Id}: Sync triggered for {Count} sections.",
                        schedule.Id, result.TotalSections);

                    // Update last run
                    schedule.LastRunAt = now;
                    await centralDb.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Schedule {Id}: Error triggering sync.", schedule.Id);
                }
            }
        }
    }
}

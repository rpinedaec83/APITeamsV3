using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Sedes;
using APITeamsV3.Application.UseCases.Teams.Commands;
using APITeamsV3.Domain.Entities;
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
    /// and triggers full sync when a schedule matches the current day/time
    /// in the tenant's configured timezone.
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

            // Capture current moment in UTC once for the whole tick
            var nowUtc = DateTime.UtcNow;

            var schedules = await centralDb.SyncSchedules
                .Where(s => s.IsEnabled)
                .ToListAsync(stoppingToken);

            foreach (var schedule in schedules)
            {
                // Resolve tenant config to get the company timezone
                var companyConfig = await centralDb.CompanyConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == schedule.CompanyConfigId, stoppingToken);

                if (companyConfig == null)
                {
                    _logger.LogWarning("Schedule {Id}: CompanyConfig {CompanyId} not found. Skipping.",
                        schedule.Id, schedule.CompanyConfigId);
                    continue;
                }

                // Convert UTC to the tenant's timezone for hour/day comparison
                var timeZoneId = string.IsNullOrWhiteSpace(companyConfig.TimeZoneId)
                    ? "SA Pacific Standard Time"
                    : companyConfig.TimeZoneId;

                TimeZoneInfo tz;
                try
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch
                {
                    tz = TimeZoneInfo.Utc;
                    _logger.LogWarning("Schedule {Id}: Unknown TimeZoneId '{TZ}'. Falling back to UTC.",
                        schedule.Id, timeZoneId);
                }

                var now = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

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

                // Check day of week
                var days = schedule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!days.Contains(currentDay)) continue;

                // Check hour and minute in tenant local time
                if (schedule.Hour != now.Hour || schedule.Minute != now.Minute) continue;

                // Avoid re-running if already ran this minute (compare against stored UTC)
                if (schedule.LastRunAt.HasValue &&
                    schedule.LastRunAt.Value.Date == nowUtc.Date &&
                    schedule.LastRunAt.Value.Hour == nowUtc.Hour &&
                    schedule.LastRunAt.Value.Minute == nowUtc.Minute)
                {
                    continue;
                }

                _logger.LogInformation("Schedule {Id} triggered for company {CompanyId} at {Time} ({TZ})",
                    schedule.Id, schedule.CompanyConfigId, now, timeZoneId);

                var execution = new SyncScheduleExecution
                {
                    SyncScheduleId = schedule.Id,
                    CompanyConfigId = schedule.CompanyConfigId,
                    Status = "Started",
                    TriggerSource = "SchedulerService",
                    TriggeredAtUtc = nowUtc,
                };
                centralDb.SyncScheduleExecutions.Add(execution);
                await centralDb.SaveChangesAsync(stoppingToken);

                try
                {
                    // Get active sede codes for this company
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var sedeCodes = await mediator.Send(new GetActiveSedeCodesQuery(schedule.CompanyConfigId, true), stoppingToken);

                    if (string.IsNullOrEmpty(sedeCodes))
                    {
                        _logger.LogWarning("Schedule {Id}: No active sedes for company {CompanyId}. Skipping.",
                            schedule.Id, schedule.CompanyConfigId);
                        execution.Status = "Skipped";
                        execution.SedeCodes = string.Empty;
                        execution.ErrorMessage = "No active sedes were found for the company.";
                        execution.CompletedAtUtc = DateTime.UtcNow;
                        await centralDb.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    // Set tenant context before dispatching the command
                    var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                    var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

                    tenantProvider.SetTenant(new TenantContext
                    {
                        CompanyKey = companyConfig.CompanyKey,
                        ConnectionString = encryptionService.Decrypt(companyConfig.SmartConnectionString),
                        TimeZoneId = companyConfig.TimeZoneId,
                        GraphTenantId = companyConfig.GraphTenantId,
                        GraphClientId = companyConfig.GraphClientId,
                        GraphClientSecret = companyConfig.GraphClientSecretRef
                    });

                    // Trigger the bulk sync
                    var result = await mediator.Send(new SyncAllTeamsCommand(sedeCodes), stoppingToken);

                    _logger.LogInformation("Schedule {Id}: Sync triggered for {Count} sections.",
                        schedule.Id, result.TotalSections);

                    // Persist last run timestamp in UTC so comparisons are timezone-independent
                    schedule.LastRunAt = nowUtc;
                    execution.Status = "Succeeded";
                    execution.SedeCodes = sedeCodes;
                    execution.TotalSections = result.TotalSections;
                    execution.EnqueuedJobsCount = result.JobIds.Count;
                    execution.JobIds = string.Join(",", result.JobIds);
                    execution.CompletedAtUtc = DateTime.UtcNow;
                    await centralDb.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Schedule {Id}: Error triggering sync.", schedule.Id);
                    execution.Status = "Failed";
                    execution.ErrorMessage = ex.Message;
                    execution.CompletedAtUtc = DateTime.UtcNow;
                    await centralDb.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }
}

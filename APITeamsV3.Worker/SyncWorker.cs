using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Services; // For SessionSchedulingService

namespace APITeamsV3.Worker
{
    public class SyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SyncWorker> _logger;

        public SyncWorker(
            IServiceProvider serviceProvider, 
            IConfiguration configuration,
            ILogger<SyncWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SyncWorker starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var centralContext = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
                        
                        // 1. Pick next pending job
                        var job = await centralContext.SyncJobs
                            .Where(j => j.Status == "Pending")
                            .OrderBy(j => j.CreatedAt)
                            .FirstOrDefaultAsync(stoppingToken);

                        if (job != null)
                        {
                            _logger.LogInformation($"Processing Job {job.Id} Type {job.JobType} for {job.CompanyKey}");
                            
                            // 2. Lock Job
                            job.Status = "Processing";
                            job.UpdatedAt = DateTime.UtcNow;
                            await centralContext.SaveChangesAsync(stoppingToken);

                            try
                            {
                                // 3. Resolve Tenant Config
                                var companyConfig = await centralContext.CompanyConfigs
                                    .FirstOrDefaultAsync(c => c.CompanyKey == job.CompanyKey, stoppingToken);

                                if (companyConfig == null)
                                    throw new Exception($"Company configuration not found for key: {job.CompanyKey}");

                                // 4. Create Tenant Context
                                var tenantContext = new TenantContext
                                {
                                    CompanyId = companyConfig.Id,
                                    CompanyKey = companyConfig.CompanyKey,
                                    DisplayName = companyConfig.DisplayName ?? string.Empty,
                                    ConnectionString = companyConfig.SmartConnectionString,
                                    TimeZoneId = companyConfig.TimeZoneId,
                                    GraphTenantId = companyConfig.GraphTenantId,
                                    GraphClientId = companyConfig.GraphClientId,
                                    GraphClientSecret = ResolveSecret(companyConfig.GraphClientSecretRef)
                                };

                                // 5. Setup Scope for Tenant
                                var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                                tenantProvider.SetTenant(tenantContext);

                                // 6. Process Job based on Type
                                switch (job.JobType)
                                {
                                    case "ProvisionTeam":
                                        var provisioningService = scope.ServiceProvider.GetRequiredService<ITeamProvisioningService>();
                                        // Payload expected to be SeccionId or JSON. Assuming TargetId has the ID.
                                        if (int.TryParse(job.TargetId, out int seccionId))
                                        {
                                            // Fetch section from SmartDB view
                                            var smartContext = scope.ServiceProvider.GetRequiredService<ISmartDbContext>();
                                            // Seccion is a View.
                                            var seccion = await smartContext.Set<Seccion>()
                                                .FirstOrDefaultAsync(s => s.IdSeccion == seccionId, stoppingToken);
                                                
                                            if (seccion == null)
                                                throw new Exception($"Section {seccionId} not found in SmartDB.");

                                            await provisioningService.ProvisionTeamAsync(seccion);
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid TargetId for ProvisionTeam: {job.TargetId}");
                                        }
                                        break;

                                    case "ScheduleSession":
                                        var schedulingService = scope.ServiceProvider.GetRequiredService<SessionSchedulingService>(); 
                                        if (int.TryParse(job.TargetId, out int sectionIdForSchedule))
                                        {
                                            await schedulingService.ScheduleRecurringSessionsAsync(sectionIdForSchedule);
                                        }
                                        break;

                                    case "SyncMissingStudents":
                                        if (int.TryParse(job.TargetId, out int sectionIdForMissing))
                                        {
                                            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                                            await mediator.Send(new APITeamsV3.Application.UseCases.Teams.Commands.SyncMissingStudentsCommand(sectionIdForMissing));
                                        }
                                        break;

                                    case "SyncObsoleteStudents":
                                        if (int.TryParse(job.TargetId, out int sectionIdForObsolete))
                                        {
                                            var mediator2 = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                                            await mediator2.Send(new APITeamsV3.Application.UseCases.Teams.Commands.SyncObsoleteStudentsCommand(sectionIdForObsolete));
                                        }
                                        break;

                                    case "SyncRenamedTeams":
                                        if (int.TryParse(job.TargetId, out int sectionIdForRenamed))
                                        {
                                            var mediator3 = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                                            await mediator3.Send(new APITeamsV3.Application.UseCases.Teams.Commands.SyncRenamedTeamsCommand(sectionIdForRenamed));
                                        }
                                        break;

                                    default:
                                        _logger.LogWarning($"Unknown JobType: {job.JobType}");
                                        break;
                                }

                                job.Status = "Completed";
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing job {job.Id}");
                                job.Status = "Failed";
                                job.LastError = ex.Message;
                                job.RetryCount++;
                                if (job.RetryCount < 3)
                                {
                                    job.Status = "Pending"; // Simple Retry
                                    // Could add exponential backoff to CreateAt/NextTry here
                                }
                            }
                            
                            job.UpdatedAt = DateTime.UtcNow;
                            await centralContext.SaveChangesAsync(stoppingToken); // Update Job Status
                        }
                        else
                        {
                             await Task.Delay(5000, stoppingToken); // idle wait
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SyncWorker outer loop");
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }

        private string ResolveSecret(string secretRef)
        {
            // Try config lookup
            var secret = _configuration[$"GraphSecrets:{secretRef}"];
            if (!string.IsNullOrEmpty(secret)) return secret;

            // Fallback: if ref looks like a secret (not placeholder), return it
            if (!secretRef.Contains("placeholder") && secretRef.Length > 20)
                return secretRef;

            return string.Empty; // Will cause auth failure, which is correct
        }
    }
}

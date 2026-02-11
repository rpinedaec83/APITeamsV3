using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using System.Linq;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.MultiTenancy
{
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, CentralDbContext dbContext, IEncryptionService encryptionService)
        {
            // 0. Exempt Admin Routes
            if (context.Request.Path.StartsWithSegments("/api/admin"))
            {
                await _next(context);
                return;
            }

            // 1. Resolve Host
            var host = context.Request.Host.Host.ToLower();
            
            // 2. Dev Override (Header)
            if (host.Contains("localhost"))
            {
                if (context.Request.Headers.TryGetValue("X-Company-Key", out var companyKeyHeader))
                {
                    var companyKey = companyKeyHeader.ToString().ToLower();
                    var devConfig = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == companyKey && c.IsActive);
                    if (devConfig != null)
                    {
                        SetTenantContext(tenantProvider, devConfig, encryptionService);
                        await _next(context);
                        return;
                    }
                }
                
                // Fallback to "idat" if no header or header not found (User's active context)
                var fallbackConfig = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == "idat" && c.IsActive);
                if (fallbackConfig != null)
                {
                     SetTenantContext(tenantProvider, fallbackConfig, encryptionService);
                     await _next(context);
                     return;
                }
            }

            // 3. Prod Resolution (Host Match)
            // Caching strategy should be applied here in production (IMemoryCache) - omitted for simplicity in initial setup
            var config = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => c.ApiHost == host && c.IsActive);

            if (config == null)
            {
                _logger.LogWarning($"Tenant resolution failed for host: {host}");
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Tenant not found or inactive.");
                return;
            }

            // 4. Set Context
            SetTenantContext(tenantProvider, config, encryptionService);

            // 5. Add to Logs
            using (_logger.BeginScope(new Dictionary<string, object> { ["CompanyKey"] = config.CompanyKey }))
            {
                await _next(context);
            }
        }

        private void SetTenantContext(ITenantProvider provider, Domain.Entities.CompanyConfig config, IEncryptionService encryptionService)
        {
            provider.SetTenant(new TenantContext
            {
                CompanyKey = config.CompanyKey,
                ConnectionString = encryptionService.Decrypt(config.SmartConnectionString), // Decrypt here
                TimeZoneId = config.TimeZoneId,
                GraphTenantId = config.GraphTenantId,
                GraphClientId = config.GraphClientId,
                GraphClientSecret = config.GraphClientSecretRef // Retrieval from Secret Store here if needed
            });
        }
    }
}

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
            // 1. Resolve Host
            var host = context.Request.Host.Host.ToLower();
            
            // 2. Resolve Tenant from Host
            APITeamsV3.Domain.Entities.CompanyConfig? config = null;

            if (host.Contains("localhost"))
            {
                if (context.Request.Headers.TryGetValue("X-Company-Key", out var companyKeyHeader))
                {
                    var companyKey = companyKeyHeader.ToString().ToLower();
                    config = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == companyKey && c.IsActive);
                }
                
                if (config == null)
                {
                    config = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == "idat" && c.IsActive);
                }
            }
            else
            {
                config = await dbContext.CompanyConfigs.FirstOrDefaultAsync(c => (c.ApiHost == host || c.FrontHost == host) && c.IsActive);
            }

            // 3. Global Maintenance Check
            var globalMaintenance = await dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GlobalMaintenanceMode");
            bool isGlobalMaintenance = globalMaintenance?.Value?.ToLower() == "true";

            // 4. Role/Tenancy Enforcement
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var roleClaims = context.User.FindAll("roles")
                    .Concat(context.User.FindAll(System.Security.Claims.ClaimTypes.Role))
                    .Select(c => c.Value)
                    .ToList();

                var email = context.User.FindFirst("preferred_username")?.Value 
                         ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

                bool isIt = roleClaims.Contains("IT");
                bool isAdmin = roleClaims.Contains("ADMIN");
                bool isAll = roleClaims.Contains("ALL");
                bool isPrivileged = isIt || isAdmin || isAll;

                // Check Global Maintenance
                if (isGlobalMaintenance && !isPrivileged)
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("El sistema se encuentra en mantenimiento global. Por favor, intente más tarde.");
                    return;
                }

                // Check Tenant Maintenance
                if (config != null && config.IsMaintenanceMode && !isPrivileged)
                {
                    context.Response.StatusCode = 503;
                    var msg = string.IsNullOrEmpty(config.MaintenanceMessage) 
                        ? $"La empresa {config.DisplayName} se encuentra en mantenimiento." 
                        : config.MaintenanceMessage;
                    await context.Response.WriteAsync(msg);
                    return;
                }

                if (isAdmin && !isIt)
                {
                    if (string.IsNullOrEmpty(email))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Forbidden: Email claim missing for role verification.");
                        return;
                    }

                    var emailDomain = email.Split('@').Last().ToLower();
                    // Block if the resolved tenant doesn't match the email domain
                    if (config != null && !config.FrontHost.ToLower().Contains(emailDomain))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync($"Forbidden: Your account ({email}) is not authorized to access this tenant ({config.CompanyKey}).");
                        return;
                    }
                }
                // GESTION role follows the host-based resolution naturally

                // Global authorization check: block users without any recognized role
                var authorizedRoles = new[] { "ADMIN", "IT", "GESTION", "ALL" };
                if (!roleClaims.Any(r => authorizedRoles.Contains(r)))
                {
                    _logger.LogWarning($"User {email} authenticated but has no authorized roles for this application.");
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Forbidden: Your account does not have sufficient roles to access this application.");
                    return;
                }
            }
            else if (isGlobalMaintenance || (config != null && config.IsMaintenanceMode))
            {
                // Public routes might still need to be accessible, but if it's maintenance, usually we block all except public API
                if (!context.Request.Path.StartsWithSegments("/api/public"))
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("Sistema en mantenimiento.");
                    return;
                }
            }

            // 4. Set Context if resolution was successful
            if (config != null)
            {
                SetTenantContext(tenantProvider, config, encryptionService);
                using (_logger.BeginScope(new Dictionary<string, object> { ["CompanyKey"] = config.CompanyKey }))
                {
                    await _next(context);
                }
            }
            else
            {
                // Exempt public routes from resolution failure
                if (context.Request.Path.StartsWithSegments("/api/public"))
                {
                    await _next(context);
                }
                else
                {
                    _logger.LogWarning($"Tenant resolution failed for host: {host}");
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Tenant not found or inactive.");
                }
            }
        }

        private void SetTenantContext(ITenantProvider provider, Domain.Entities.CompanyConfig config, IEncryptionService encryptionService)
        {
            string connectionString;
            try
            {
                connectionString = encryptionService.Decrypt(config.SmartConnectionString);
            }
            catch (Exception)
            {
                connectionString = config.SmartConnectionString;
            }

            provider.SetTenant(new TenantContext
            {
                CompanyId = config.Id,
                CompanyKey = config.CompanyKey,
                DisplayName = config.DisplayName,
                ConnectionString = connectionString,
                TimeZoneId = config.TimeZoneId,
                GraphTenantId = config.GraphTenantId,
                GraphClientId = config.GraphClientId,
                GraphClientSecret = config.GraphClientSecretRef, // Retrieval from Secret Store here if needed
                SpaClientId = config.SpaClientId ?? string.Empty,
                SpaTenantId = config.SpaTenantId ?? config.GraphTenantId, // Assumed same tenant for Graph and SPA if missing
                IsMaintenanceMode = config.IsMaintenanceMode,
                MaintenanceMessage = config.MaintenanceMessage
            });
        }
    }
}

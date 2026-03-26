using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Domain.Entities;
using System.Linq;

namespace APITeamsV3.API.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicConfigController : ControllerBase
    {
        private readonly CentralDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public PublicConfigController(CentralDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpGet("spa-config")]
        public async Task<IActionResult> GetSpaConfig()
        {
            try
            {
                // Resolve tenant by the API host (e.g. api.teams.zegel.edu.pe)
                var host = Request.Host.Host.ToLower();

                var config = await _dbContext.CompanyConfigs
                    .FirstOrDefaultAsync(c => c.ApiHost == host && c.IsActive);

                if (config == null)
                    return NotFound($"No tenant config found for host: {host}");

                return Ok(new
                {
                    spaClientId = config.SpaClientId ?? string.Empty,
                    tenantId = config.SpaTenantId ?? config.GraphTenantId,
                    companyKey = config.CompanyKey,
                    apiClientId = config.ApiClientId ?? _configuration["AzureAd:ClientId"] ?? string.Empty,
                    apiScopes = GetApiScopes(config)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Error resolving tenant config.");
            }
        }

        private string[] GetApiScopes(CompanyConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.ApiScopes))
            {
                return config.ApiScopes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();
            }

            var configuredScopes = _configuration.GetSection("AzureAd:Scopes").Get<string[]>();
            if (configuredScopes is { Length: > 0 })
            {
                return configuredScopes;
            }

            var apiClientId = config.ApiClientId ?? _configuration["AzureAd:ClientId"];
            if (!string.IsNullOrWhiteSpace(config.ApiScopes))
            {
                return config.ApiScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            return new[] { $"{apiClientId}/.default" };
        }
    }
}


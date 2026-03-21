using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;

namespace APITeamsV3.API.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicConfigController : ControllerBase
    {
        private readonly CentralDbContext _dbContext;

        public PublicConfigController(CentralDbContext dbContext)
        {
            _dbContext = dbContext;
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
                    tenantId    = config.SpaTenantId ?? config.GraphTenantId,
                    companyKey  = config.CompanyKey
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error resolving tenant config: {ex.Message}");
            }
        }
    }
}


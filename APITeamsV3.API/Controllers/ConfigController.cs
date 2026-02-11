using Microsoft.AspNetCore.Mvc;
using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/config")]
    public class ConfigController : ControllerBase
    {
        private readonly ITenantProvider _tenantProvider;

        public ConfigController(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        public IActionResult GetConfig()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            return Ok(new 
            { 
                ClientId = tenant.GraphClientId,
                Authority = $"https://login.microsoftonline.com/{tenant.GraphTenantId}",
                RedirectUri = $"https://{Request.Host}/auth/callback",
                CompanyKey = tenant.CompanyKey
            });
        }
    }
}

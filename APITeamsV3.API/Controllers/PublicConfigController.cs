using Microsoft.AspNetCore.Mvc;
using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.API.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicConfigController : ControllerBase
    {
        private readonly ITenantProvider _tenantProvider;

        public PublicConfigController(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }

        [HttpGet("spa-config")]
        public IActionResult GetSpaConfig()
        {
            try
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                return Ok(new
                {
                    spaClientId = tenant.SpaClientId,
                    tenantId = tenant.SpaTenantId,
                    companyKey = tenant.CompanyKey
                });
            }
            catch (System.Exception)
            {
                return NotFound("Tenant config not found");
            }
        }
    }
}

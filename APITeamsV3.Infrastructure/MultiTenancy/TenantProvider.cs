using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.Infrastructure.MultiTenancy
{
    public class TenantProvider : ITenantProvider
    {
        private TenantContext? _currentTenant;

        public TenantContext GetCurrentTenant()
        {
            return _currentTenant ?? throw new System.Exception("Tenant context is not initialized.");
        }

        public void SetTenant(TenantContext context)
        {
            _currentTenant = context;
        }
    }
}

using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.Infrastructure.MultiTenancy
{
    public class TenantProvider : ITenantProvider
    {
        private readonly IEncryptionService _encryptionService;
        private TenantContext? _currentTenant;

        public TenantProvider(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        public TenantContext GetCurrentTenant()
        {
            return _currentTenant ?? throw new System.Exception("Tenant context is not initialized.");
        }

        public void SetTenant(TenantContext context)
        {
            if (context != null && !string.IsNullOrEmpty(context.GraphClientSecret) && context.GraphClientSecret.StartsWith("v2:"))
            {
                try
                {
                    context.GraphClientSecret = _encryptionService.Decrypt(context.GraphClientSecret);
                }
                catch
                {
                    // Keep the encrypted value if decryption fails for any reason
                }
            }
            _currentTenant = context;
        }
    }
}

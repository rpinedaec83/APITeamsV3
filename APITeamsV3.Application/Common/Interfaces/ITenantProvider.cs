namespace APITeamsV3.Application.Common.Interfaces
{
    public class TenantContext
    {
        public string CompanyKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string GraphTenantId { get; set; } = string.Empty;
        public string GraphClientId { get; set; } = string.Empty;
        public string GraphClientSecret { get; set; } = string.Empty; // Decrypted/Retrieved secret
    }

    public interface ITenantProvider
    {
        TenantContext GetCurrentTenant();
        void SetTenant(TenantContext context);
    }
}

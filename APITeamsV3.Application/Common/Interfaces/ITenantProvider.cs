namespace APITeamsV3.Application.Common.Interfaces
{
    public class TenantContext
    {
        public int CompanyId { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string GraphTenantId { get; set; } = string.Empty;
        public string GraphClientId { get; set; } = string.Empty;
        public string GraphClientSecret { get; set; } = string.Empty; // Decrypted/Retrieved secret
        public string SpaClientId { get; set; } = string.Empty; // For Frontend MSAL
        public string SpaTenantId { get; set; } = string.Empty; // For Frontend MSAL
        
        public bool IsMaintenanceMode { get; set; }
        public string? MaintenanceMessage { get; set; }
    }

    public interface ITenantProvider
    {
        TenantContext GetCurrentTenant();
        void SetTenant(TenantContext context);
    }
}

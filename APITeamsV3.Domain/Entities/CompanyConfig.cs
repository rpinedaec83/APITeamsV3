using System;

namespace APITeamsV3.Domain.Entities
{
    public class CompanyConfig
    {
        public int Id { get; set; }
        public string CompanyKey { get; set; } = string.Empty; // zegel, idat, etc.
        public string DisplayName { get; set; } = string.Empty;
        public string FrontHost { get; set; } = string.Empty; // teams.zegel.edu.pe
        public string ApiHost { get; set; } = string.Empty; // api.teams.zegel.edu.pe
        public string? SpaClientId { get; set; }
        public string? SpaTenantId { get; set; } // Explicit Tenant ID for SPA auth
        public string? ApiClientId { get; set; }
        public string? ApiScopes { get; set; } // Comma-separated scopes
        public string SmartConnectionString { get; set; } = string.Empty; // Encrypted or safe ref
        public string TimeZoneId { get; set; } = "SA Pacific Standard Time";
        public bool IsActive { get; set; } = true;
        public DateTime LastSyncTimestamp { get; set; }

        // Graph Config
        public string GraphTenantId { get; set; } = string.Empty;
        public string GraphClientId { get; set; } = string.Empty;
        public string GraphClientSecretRef { get; set; } = string.Empty; // Reference to secret store
        
        // Policies
        public string DefaultChannelName { get; set; } = "General";
        public string MeetingPolicyMode { get; set; } = "app-only";
    }
}

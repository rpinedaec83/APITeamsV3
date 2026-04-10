using System;

namespace APITeamsV3.Domain.Entities
{
    public class SystemSetting
    {
        public string Key { get; set; } = string.Empty; // e.g. "GlobalMaintenanceMode"
        public string Value { get; set; } = string.Empty; // e.g. "true" or "false"
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

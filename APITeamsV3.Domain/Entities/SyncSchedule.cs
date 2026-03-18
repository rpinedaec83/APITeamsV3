using System;

namespace APITeamsV3.Domain.Entities
{
    public class SyncSchedule
    {
        public int Id { get; set; }
        public int CompanyConfigId { get; set; }

        // Days of week (stored as comma-separated: "Mon,Wed,Fri")
        public string DaysOfWeek { get; set; } = string.Empty;

        // Time of day (24h format)
        public int Hour { get; set; }
        public int Minute { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastRunAt { get; set; }
    }
}

using System;

namespace APITeamsV3.Domain.Entities
{
    public class SyncJob
    {
        public int Id { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty; // Provision, Roster, Schedule
        public string TargetId { get; set; } = string.Empty; // IdSeccion
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

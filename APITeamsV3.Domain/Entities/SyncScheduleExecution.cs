using System;

namespace APITeamsV3.Domain.Entities
{
    public class SyncScheduleExecution
    {
        public int Id { get; set; }
        public int SyncScheduleId { get; set; }
        public int CompanyConfigId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TriggerSource { get; set; } = "SchedulerService";
        public string SedeCodes { get; set; } = string.Empty;
        public int TotalSections { get; set; }
        public int EnqueuedJobsCount { get; set; }
        public string JobIds { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime TriggeredAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
    }
}

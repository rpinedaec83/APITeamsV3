using System;
using System.Collections.Generic;

namespace APITeamsV3.Application.Common.Models
{
    public class RecordingTransferRequest
    {
        public string? JobId { get; set; }
        public string? ExecutedBy { get; set; }
        public string? OrganizerUserPrincipalName { get; set; }
        public string? OrganizerUserId { get; set; }
        public string TeamGroupId { get; set; } = string.Empty;
        public string? ChannelName { get; set; }
        public string? DestinationFolderPath { get; set; }
        public string? SourceFolderPath { get; set; }
        public string? CourseName { get; set; }
        public string? Section { get; set; }
        public int? SectionId { get; set; }
        public string? SectionCode { get; set; }
        public DateTimeOffset? StartDateUtc { get; set; }
        public DateTimeOffset? EndDateUtc { get; set; }
        public int? LookBackHours { get; set; }
        public int? MaxFiles { get; set; }
    }

    public class RecordingTransferResult
    {
        public string OrganizerResolvedId { get; set; } = string.Empty;
        public string OrganizerResolvedUserPrincipalName { get; set; } = string.Empty;
        public string TeamGroupId { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public DateTimeOffset WindowStartUtc { get; set; }
        public DateTimeOffset WindowEndUtc { get; set; }
        public int FilesFound { get; set; }
        public int FilesCopied { get; set; }
        public int FilesSkipped { get; set; }
        public int FilesErrored { get; set; }
        public int SourceFilesDeleted { get; set; }
        public int SourceFilesDeleteErrors { get; set; }
        public List<string> Warnings { get; set; } = [];
        public List<string> Errors { get; set; } = [];
        public List<RecordingTransferFileResult> Files { get; set; } = [];
    }

    public class RecordingTransferFileResult
    {
        public string SourceItemId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string SourceWebUrl { get; set; } = string.Empty;
        public DateTimeOffset? SourceLastModifiedUtc { get; set; }
        public string DestinationName { get; set; } = string.Empty;
        public string DestinationItemId { get; set; } = string.Empty;
        public string DestinationWebUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Copied | Skipped | Error
        public string Message { get; set; } = string.Empty;
        public bool SourceDeleted { get; set; }
    }

    public class DriveQuotaResult
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long RemainingBytes { get; set; }
        public double PercentAvailable { get; set; }
        public string State { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}

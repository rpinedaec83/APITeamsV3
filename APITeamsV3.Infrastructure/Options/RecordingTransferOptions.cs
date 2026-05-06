namespace APITeamsV3.Infrastructure.Options
{
    public class RecordingTransferOptions
    {
        public const string SectionName = "RecordingTransfer";

        public string DefaultChannelName { get; set; } = "General";
        public string SourceFolderPath { get; set; } = "Recordings";
        public string DestinationSubFolderName { get; set; } = "Recordings";
        public int DefaultLookBackHours { get; set; } = 48;
        public int MaxFilesPerRun { get; set; } = 25;
        public int ListPageSize { get; set; } = 200;
        public int CopyPollingIntervalSeconds { get; set; } = 5;
        public int CopyPollingTimeoutSeconds { get; set; } = 180;
        public bool SkipIfFriendlyNameAlreadyExists { get; set; } = true;
        public bool DeleteSourceAfterCopy { get; set; } = true;
        public bool RequireSectionTokenMatch { get; set; } = true;
    }
}

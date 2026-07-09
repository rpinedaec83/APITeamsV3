namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IHangfireJobService
    {
        Task<string> EnqueueSyncRoster(int idSeccion, bool fullSync, string? executedBy = null);
        Task<string> EnqueueSyncDates(int idSeccion, string? executedBy = null);
        Task<string> EnqueueSyncFacilitator(int idSeccion, string? executedBy = null);
        Task<string> EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento, string? executedBy = null);
        Task<string> EnqueueGenerateSchedule(int idSeccion, string? executedBy = null);
        Task<string> EnqueueSyncMissingStudents(int idSeccion, string? executedBy = null);
        Task<string> EnqueueSyncObsoleteStudents(int idSeccion, string? executedBy = null);
        Task<string> EnqueueSyncRenamedTeams(int idSeccion, string? executedBy = null);
        Task<string> EnqueueFullSectionSync(int idSeccion, string? executedBy = null);
        Task<string> EnqueueSyncSectionTeam(int idSeccion, string? executedBy = null);
        Task<string> EnqueuePilotRecordingTransfers(string companyKey, string? executedBy = null);
        Task<string> EnqueueAllRecordingTransfers(string companyKey, string? executedBy = null);
        Task<string> EnqueueRecordingTransferForSection(int idSeccion, string companyKey, string? executedBy = null);
        Task<string> EnqueueSyncAttendance(int idSeccion, string? executedBy = null);
        Task<string> EnqueueCheckStorageQuota(string? executedBy = null);
    }
}

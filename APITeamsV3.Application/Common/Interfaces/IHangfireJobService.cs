namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IHangfireJobService
    {
        Task<string> EnqueueSyncRoster(int idSeccion, bool fullSync);
        Task<string> EnqueueSyncDates(int idSeccion);
        Task<string> EnqueueSyncFacilitator(int idSeccion);
        Task<string> EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento);
        Task<string> EnqueueGenerateSchedule(int idSeccion);
        Task<string> EnqueueSyncMissingStudents(int idSeccion);
        Task<string> EnqueueSyncObsoleteStudents(int idSeccion);
        Task<string> EnqueueSyncRenamedTeams(int idSeccion);
        Task<string> EnqueueFullSectionSync(int idSeccion);
        Task<string> EnqueueSyncSectionTeam(int idSeccion);
        Task<string> EnqueuePilotRecordingTransfers(string companyKey);
    }
}

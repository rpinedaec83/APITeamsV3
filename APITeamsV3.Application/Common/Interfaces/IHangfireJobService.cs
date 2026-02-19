namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IHangfireJobService
    {
        string EnqueueSyncRoster(int idSeccion, bool fullSync);
        string EnqueueSyncDates(int idSeccion);
        string EnqueueSyncFacilitator(int idSeccion);
        string EnqueueUpdateJoinUrl(int idSeccion, string joinUrl, string idEvento);
        string EnqueueGenerateSchedule(int idSeccion);
        string EnqueueSyncMissingStudents(int idSeccion);
        string EnqueueSyncObsoleteStudents(int idSeccion);
        string EnqueueSyncRenamedTeams(int idSeccion);
    }
}

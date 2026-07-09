using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionAttendanceCommand : IRequest<SyncSectionAttendanceResult>
    {
        public int IdSeccion { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? ExecutedBy { get; set; }

        public SyncSectionAttendanceCommand(int idSeccion, string companyKey, string? jobId = null, string? executedBy = null)
        {
            IdSeccion = idSeccion;
            CompanyKey = companyKey;
            JobId = jobId;
            ExecutedBy = executedBy;
        }
    }

    public class SyncSectionAttendanceResult
    {
        public bool Success { get; set; }
        public int TotalMeetingsSynced { get; set; }
        public int TotalParticipantsSaved { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

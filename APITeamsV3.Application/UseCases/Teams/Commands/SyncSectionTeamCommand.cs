using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionTeamCommand : IRequest<SyncSectionTeamResult>
    {
        public int IdSeccion { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? ExecutedBy { get; set; }

        public SyncSectionTeamCommand(int idSeccion, string companyKey, string? jobId = null, string? executedBy = null)
        {
            IdSeccion = idSeccion;
            CompanyKey = companyKey;
            JobId = jobId;
            ExecutedBy = executedBy;
        }
    }

    public class SyncSectionTeamResult
    {
        public int Success { get; set; }
        public int Failure { get; set; }
        public int Ignored { get; set; }
    }
}

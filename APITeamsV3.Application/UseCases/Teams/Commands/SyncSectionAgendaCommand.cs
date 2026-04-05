using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionAgendaCommand : IRequest<DiagnosticResultDto>
    {
        public int IdSeccion { get; }
        public string CompanyKey { get; }
        public string? JobId { get; }

        public SyncSectionAgendaCommand(int idSeccion, string companyKey, string? jobId = null)
        {
            IdSeccion = idSeccion;
            CompanyKey = companyKey;
            JobId = jobId;
        }
    }
}

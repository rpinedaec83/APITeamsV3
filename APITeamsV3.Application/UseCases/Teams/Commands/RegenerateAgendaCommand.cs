using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RegenerateAgendaCommand : IRequest<DiagnosticResultDto>
    {
        public int IdSeccion { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? ExecutedBy { get; set; }

        public RegenerateAgendaCommand(int idSeccion, string companyKey, string? jobId = null, string? executedBy = null)
        {
            IdSeccion = idSeccion;
            CompanyKey = companyKey;
            JobId = jobId;
            ExecutedBy = executedBy;
        }
    }
}

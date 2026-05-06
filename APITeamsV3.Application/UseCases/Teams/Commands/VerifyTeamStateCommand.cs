using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class VerifyTeamStateCommand : IRequest<DiagnosticResultDto>
    {
        public int IdSeccion { get; set; }
        public string? JobId { get; set; }
        public string? ExecutedBy { get; set; }

        public VerifyTeamStateCommand(int idSeccion, string? jobId = null, string? executedBy = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
            ExecutedBy = executedBy;
        }
    }

    public class DiagnosticResultDto
    {
        public bool IsValid { get; set; }
        public string Summary { get; set; } = string.Empty;
        public bool ReactivatedLocally { get; set; }
        public bool MetadataAutoCorrected { get; set; }
        public bool MarkedInactiveLocally { get; set; }
        public bool TeamExistsInGraph { get; set; }
    }
}

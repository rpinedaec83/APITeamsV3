using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class VerifyTeamStateCommand : IRequest<DiagnosticResultDto>
    {
        public int IdSeccion { get; set; }
        public string? JobId { get; set; }

        public VerifyTeamStateCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }

    public class DiagnosticResultDto
    {
        public bool IsValid { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}

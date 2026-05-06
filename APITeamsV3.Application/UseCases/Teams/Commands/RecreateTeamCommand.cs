using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RecreateTeamCommand : IRequest<RecreateTeamResult>
    {
        public int IdSeccion { get; set; }
        public string CompanyKey { get; set; } = string.Empty;
        public string? ExecutedBy { get; set; }

        public RecreateTeamCommand(int idSeccion, string companyKey, string? executedBy = null)
        {
            IdSeccion = idSeccion;
            CompanyKey = companyKey;
            ExecutedBy = executedBy;
        }
    }

    public class RecreateTeamResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? NewTeamId { get; set; }
    }
}

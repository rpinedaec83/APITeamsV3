using System.Collections.Generic;
using MediatR;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class RegeneratePilotAgendasResult
    {
        public bool IsValid { get; set; }
        public string Summary { get; set; } = string.Empty;
        public int TotalSections { get; set; }
        public int SucceededSections { get; set; }
        public int FailedSections { get; set; }
        public List<string> FailedSectionCodes { get; set; } = new();
    }

    public class RegeneratePilotAgendasCommand : IRequest<RegeneratePilotAgendasResult>
    {
        public string CompanyKey { get; }
        public string? ExecutedBy { get; }

        public RegeneratePilotAgendasCommand(string companyKey, string? executedBy = null)
        {
            CompanyKey = companyKey;
            ExecutedBy = executedBy;
        }
    }
}

using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    public class ObsoleteTeamProcessedDto
    {
        public int IdSeccionSmart { get; set; }
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public bool GraphDeleted { get; set; }
        public string? GraphError { get; set; }
    }

    public class SyncObsoleteTeamsResult
    {
        public int TotalObsoleteFound { get; set; }
        public List<ObsoleteTeamProcessedDto> ProcessedTeams { get; set; } = new();
    }
}

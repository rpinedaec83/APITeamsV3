using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    public class SyncAllTeamsResult
    {
        public int TotalSections { get; set; }
        public List<int> SectionIds { get; set; } = new();
        public List<string> JobIds { get; set; } = new();
    }
}

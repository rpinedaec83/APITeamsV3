using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport
{
    public class DetailedReportResultDto
    {
        public int TotalRecords { get; set; }
        public List<DetailedReportDto> Data { get; set; } = new List<DetailedReportDto>();
    }
}

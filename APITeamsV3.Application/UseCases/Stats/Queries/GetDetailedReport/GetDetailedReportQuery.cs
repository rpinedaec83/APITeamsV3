using MediatR;

namespace APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport
{
    public class GetDetailedReportQuery : IRequest<DetailedReportResultDto>
    {
        public string? SmartSearch { get; set; }
        public string? Unidad { get; set; }
        public string? Programa { get; set; }
        public string? Periodo { get; set; }
        public string? Sede { get; set; }
        public string? Docente { get; set; }
        public string? Alumno { get; set; }
        
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}

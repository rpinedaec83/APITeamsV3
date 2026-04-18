using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using System;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetLogSummaryQuery : IRequest<LogOperationalSummaryDto>
    {
        public string? TipoFiltro { get; set; }
        public string? SeveridadFiltro { get; set; }
        public string? EntidadFiltro { get; set; }
        public string? ReferenciaFiltro { get; set; }
        public string? JobIdFiltro { get; set; }
        public DateTime? FechaDesde { get; set; }
        public string? SearchTerm { get; set; }
        public string? CompanyKey { get; set; }
        public string? Scope { get; set; }
    }
}

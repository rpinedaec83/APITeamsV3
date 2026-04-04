using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;
using System;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetLogsQuery : IRequest<List<TeamsLogOperativoDto>>
    {
        // Paginacion y filtros
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? TipoFiltro { get; set; }
        public string? SeveridadFiltro { get; set; }
        public string? EntidadFiltro { get; set; }
        public string? ReferenciaFiltro { get; set; }
        public string? JobIdFiltro { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? FechaDesde { get; set; }
    }
}

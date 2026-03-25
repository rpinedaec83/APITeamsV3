using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;
using System;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetLogsQuery : IRequest<List<TeamsLogOperativoDto>>
    {
        // Paginación y filtros básicos
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? TipoFiltro { get; set; }
        public DateTime? FechaDesde { get; set; }
    }
}

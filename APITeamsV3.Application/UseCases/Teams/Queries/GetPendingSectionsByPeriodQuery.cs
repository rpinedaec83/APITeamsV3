using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetPendingSectionsByPeriodQuery : IRequest<List<PendingSectionDto>>
    {
        public string CodigoPeriodo { get; set; }
        public string? FechaInicioClases { get; set; }

        public GetPendingSectionsByPeriodQuery(string codigoPeriodo, string? fechaInicioClases = null)
        {
            CodigoPeriodo = codigoPeriodo;
            FechaInicioClases = fechaInicioClases;
        }
    }
}

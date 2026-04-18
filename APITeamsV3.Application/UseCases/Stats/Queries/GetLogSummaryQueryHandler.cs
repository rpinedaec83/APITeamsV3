using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetLogSummaryQueryHandler : IRequestHandler<GetLogSummaryQuery, LogOperationalSummaryDto>
    {
        private readonly ISmartDbContext _context;

        public GetLogSummaryQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<LogOperationalSummaryDto> Handle(GetLogSummaryQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Set<APITeamsV3.Domain.Entities.TeamsLogOperativo>().AsNoTracking();

            // Apply filters (same as GetLogsQueryHandler)
            if (!string.IsNullOrWhiteSpace(request.TipoFiltro))
            {
                var tipoFiltro = request.TipoFiltro.Trim();
                query = query.Where(l => l.Tipo == tipoFiltro);
            }

            if (!string.IsNullOrWhiteSpace(request.SeveridadFiltro))
            {
                var severidadFiltro = request.SeveridadFiltro.Trim();
                query = query.Where(l => l.Severidad == severidadFiltro);
            }

            if (!string.IsNullOrWhiteSpace(request.EntidadFiltro))
            {
                var entidadFiltro = request.EntidadFiltro.Trim();
                query = query.Where(l => l.EntidadAfectada == entidadFiltro);
            }

            if (!string.IsNullOrWhiteSpace(request.ReferenciaFiltro))
            {
                var referenciaFiltro = request.ReferenciaFiltro.Trim();
                query = query.Where(l => (l.Referencia ?? string.Empty).Contains(referenciaFiltro));
            }

            if (!string.IsNullOrWhiteSpace(request.JobIdFiltro))
            {
                var jobIdFiltro = request.JobIdFiltro.Trim();
                query = query.Where(l => (l.JobId ?? string.Empty).Contains(jobIdFiltro));
            }

            if (request.FechaDesde.HasValue)
            {
                query = query.Where(l => l.Fecha >= request.FechaDesde.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.Trim();
                query = query.Where(l =>
                    (l.Tipo ?? string.Empty).Contains(search) ||
                    (l.EntidadAfectada ?? string.Empty).Contains(search) ||
                    (l.Referencia ?? string.Empty).Contains(search) ||
                    (l.Mensaje ?? string.Empty).Contains(search) ||
                    (l.Severidad ?? string.Empty).Contains(search) ||
                    (l.JobId ?? string.Empty).Contains(search) ||
                    (l.ContextoTecnico ?? string.Empty).Contains(search));
            }

            // Group by EntidadAfectada and Tipo to get counts
            var summaryData = await query
                .GroupBy(l => new { l.EntidadAfectada, l.Tipo })
                .Select(g => new
                {
                    Entidad = g.Key.EntidadAfectada,
                    Tipo = g.Key.Tipo,
                    Count = g.Count()
                })
                .ToListAsync(cancellationToken);

            var result = new LogOperationalSummaryDto();

            foreach (var item in summaryData)
            {
                if (item.Tipo == "Success")
                {
                    if (item.Entidad == "Student" || item.Entidad == "StudentSync" || item.Entidad == "Members")
                        result.StudentsSuccess += item.Count;
                    else if (item.Entidad == "Agenda")
                        result.AgendasSuccess += item.Count;
                    else if (item.Entidad == "Team")
                        result.TeamsSuccess += item.Count;
                }
                
                if (item.Tipo == "Error") result.TotalErrors += item.Count;
                if (item.Tipo == "Warning") result.TotalWarnings += item.Count;
            }

            return result;
        }
    }
}

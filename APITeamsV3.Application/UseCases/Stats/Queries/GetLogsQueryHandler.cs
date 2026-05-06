using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetLogsQueryHandler : IRequestHandler<GetLogsQuery, List<TeamsLogOperativoDto>>
    {
        private readonly ISmartDbContext _context;

        public GetLogsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<TeamsLogOperativoDto>> Handle(GetLogsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Set<APITeamsV3.Domain.Entities.TeamsLogOperativo>().AsNoTracking();

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
                    (l.Usuario ?? string.Empty).Contains(search) ||
                    (l.ContextoTecnico ?? string.Empty).Contains(search));
            }

            query = query.OrderByDescending(l => l.Fecha)
                         .ThenByDescending(l => l.Id)
                         .Skip((request.Page - 1) * request.PageSize)
                         .Take(request.PageSize);

            var logs = await query.Select(l => new TeamsLogOperativoDto
            {
                Id = l.Id,
                CompanyKey = string.Empty,
                Tipo = l.Tipo,
                EntidadAfectada = l.EntidadAfectada,
                Referencia = l.Referencia,
                Mensaje = l.Mensaje,
                ContextoTecnico = l.ContextoTecnico,
                Severidad = l.Severidad,
                JobId = l.JobId ?? string.Empty,
                Usuario = l.Usuario ?? string.Empty,
                Fecha = l.Fecha
            }).ToListAsync(cancellationToken);

            return logs;
        }
    }
}

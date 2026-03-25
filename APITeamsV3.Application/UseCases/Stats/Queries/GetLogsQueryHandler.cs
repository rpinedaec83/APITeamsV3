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

            if (!string.IsNullOrEmpty(request.TipoFiltro))
            {
                query = query.Where(l => l.Tipo == request.TipoFiltro);
            }

            if (request.FechaDesde.HasValue)
            {
                query = query.Where(l => l.Fecha >= request.FechaDesde.Value);
            }

            query = query.OrderByDescending(l => l.Fecha)
                         .Skip((request.Page - 1) * request.PageSize)
                         .Take(request.PageSize);

            var logs = await query.Select(l => new TeamsLogOperativoDto
            {
                Id = l.Id,
                Tipo = l.Tipo,
                EntidadAfectada = l.EntidadAfectada,
                Referencia = l.Referencia,
                Mensaje = l.Mensaje,
                ContextoTecnico = l.ContextoTecnico,
                Severidad = l.Severidad,
                JobId = l.JobId ?? string.Empty,
                Fecha = l.Fecha
            }).ToListAsync(cancellationToken);

            return logs;
        }
    }
}

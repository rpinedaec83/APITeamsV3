using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionsQueryHandler : IRequestHandler<GetSectionsQuery, List<SectionDetailDto>>
    {
        private readonly ISmartDbContext _context;

        public GetSectionsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<SectionDetailDto>> Handle(GetSectionsQuery request, CancellationToken cancellationToken)
        {
            var sections = await _context.Set<Seccion>()
                .AsNoTracking()
                .OrderByDescending(s => s.IdSeccion)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Fetch generic Team status for these (optimization: do in separate query or join if possible)
            var sectionIds = sections.Select(s => s.IdSeccion).ToList();
            var teams = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .Where(t => sectionIds.Contains(t.IdSeccionSmart) && t.EstadoTeam == "A")
                .Select(t => t.IdSeccionSmart)
                .ToListAsync(cancellationToken);
            
            return sections.Select(section => new SectionDetailDto
            {
                IdSeccion = section.IdSeccion,
                Codigo = section.GrupoCodigo,
                Sede = section.SedeNombre,
                Producto = section.ProductoNombre,
                Curso = section.CursoNombre,
                Profesor = $"{section.NombresFacilitador}",
                Members = new List<StudentSummaryDto>(), // Skip members for list view
                HasTeam = teams.Contains(section.IdSeccion),
                EsTeams = section.EsTeams
            }).ToList();
        }
    }
}

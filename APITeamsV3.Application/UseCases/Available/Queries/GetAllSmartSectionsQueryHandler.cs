using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Available.Queries
{
    public class GetAllSmartSectionsQueryHandler : IRequestHandler<GetAllSmartSectionsQuery, List<Seccion>>
    {
        private readonly ISmartDbContext _context;

        public GetAllSmartSectionsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<Seccion>> Handle(GetAllSmartSectionsQuery request, CancellationToken cancellationToken)
        {
            // Maps to vw_MatriculasActivas
            return await _context.Set<Seccion>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}

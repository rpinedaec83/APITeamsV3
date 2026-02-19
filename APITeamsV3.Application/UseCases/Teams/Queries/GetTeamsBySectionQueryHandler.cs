using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetTeamsBySectionQueryHandler : IRequestHandler<GetTeamsBySectionQuery, List<TeamBySectionDto>>
    {
        private readonly ISmartDbContext _context;

        public GetTeamsBySectionQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<TeamBySectionDto>> Handle(GetTeamsBySectionQuery request, CancellationToken cancellationToken)
        {
            // Option 8 Logic: Traemos los equipos creados
            var sql = @"
                SELECT *
                FROM TeamsEquipos WITH(NOLOCK)
                WHERE IdSeccionSmart = {0}";

            return await _context.Database.SqlQueryRaw<TeamBySectionDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

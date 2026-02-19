using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetCoursesMissingFacilitatorQueryHandler : IRequestHandler<GetCoursesMissingFacilitatorQuery, List<CourseMissingFacilitatorDto>>
    {
        private readonly ISmartDbContext _context;

        public GetCoursesMissingFacilitatorQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<CourseMissingFacilitatorDto>> Handle(GetCoursesMissingFacilitatorQuery request, CancellationToken cancellationToken)
        {
            // Option 3 Logic: TRAE LOS CURSOS QUE FALTA ASIGNAR FACILITADOR
            var sql = @"
                SELECT DISTINCT TE.IdTeamsGroup AS 'IdTeam',
                  MPG.CodigoFacilitador,
                  MPG.NombresFacilitador,
                  MPG.ApellidosFacilitador
                FROM TeamsProgramacionGeneral MPG WITH (NOLOCK)
                  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = MPG.IdCurso
                  AND TE.EstadoTeam = 'A'
                WHERE TE.IdSeccionSmart = {0}
                  AND NOT EXISTS (
                    SELECT 1
                    FROM TeamsEquipos te WITH (NOLOCK)
                    WHERE te.IdSeccionSmart = {0}
                      AND te.EstadoTeam = 'A'
                      AND te.Propietario3 <> MPG.CodigoFacilitador
                  )";

            return await _context.Database.SqlQueryRaw<CourseMissingFacilitatorDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetObsoleteStudentsQueryHandler : IRequestHandler<GetObsoleteStudentsQuery, List<ObsoleteStudentDto>>
    {
        private readonly ISmartDbContext _context;

        public GetObsoleteStudentsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<ObsoleteStudentDto>> Handle(GetObsoleteStudentsQuery request, CancellationToken cancellationToken)
        {
            // Option 5 Logic:
            var sql = @"
                WITH dtOldMembers AS (
                  SELECT TE.IdTeamsGroup,
                    TU.CodigoAlumno
                  FROM TeamsUsuarios TU WITH (NOLOCK)
                    LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)
                  WHERE TE.IdSeccionSmart = {0}
                    AND NOT EXISTS (
                      SELECT 1
                      FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)
                      WHERE MPG.IdCurso = TE.IdSeccionSmart
                        AND MPG.CodigoAlumno = TU.CodigoAlumno
                    )
                    AND TU.Estado = 'A'
                    AND TU.Tipo = 'A'
                    AND TE.EstadoTeam = 'A'
                )
                SELECT *
                FROM dtOldMembers
                WHERE ISNULL(IdTeamsGroup, '') <> ''";

            return await _context.Database.SqlQueryRaw<ObsoleteStudentDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

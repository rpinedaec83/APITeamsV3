using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetMissingStudentsQueryHandler : IRequestHandler<GetMissingStudentsQuery, List<MissingStudentDto>>
    {
        private readonly ISmartDbContext _context;

        public GetMissingStudentsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<MissingStudentDto>> Handle(GetMissingStudentsQuery request, CancellationToken cancellationToken)
        {
            // Option 4 Logic: TRAE LOS ALUMNOS QUE AUN NO HAN SIDO AGREGADOS AL TEAMS
            var sql = @"
                SELECT TE.IdTeamsGroup,
                  MPG.CodigoAlumno,
                  MPG.NombresAlumno,
                  MPG.ApellidosAlumno,
                  MPG.EmailAlumno
                FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)
                  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)
                WHERE TE.IdSeccionSmart = {0}
                  AND NOT EXISTS (
                    SELECT 1
                    FROM TeamsUsuarios TU WITH (NOLOCK)
                    WHERE TU.CodigoAlumno = MPG.CodigoAlumno
                      AND TU.idTeams = TE.IdTeamsGroup
                      AND TU.Tipo = 'A'
                      AND TU.Estado = 'A'
                  )
                  AND ISNULL(MPG.CodigoFacilitador, '') <> ''
                  AND TE.EstadoTeam = 'A'";

            return await _context.Database.SqlQueryRaw<MissingStudentDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

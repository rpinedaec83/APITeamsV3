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
            var sql = @"
SELECT DISTINCT
    TE.IdTeamsGroup,
    CodigoAlumno = COALESCE(NULLIF(AL.Codigo, ''), CONVERT(VARCHAR(50), AL.IdAlumno)),
    NombresAlumno = ISNULL(AL.Nombre, ''),
    ApellidosAlumno = '',
    EmailAlumno = NULLIF(AL.EmailInstitucion, '')
FROM AlumnoCurso AC WITH (NOLOCK)
INNER JOIN Alumno AL WITH (NOLOCK)
    ON AL.IdAlumno = AC.IdAlumno
INNER JOIN TeamsEquipos TE WITH (NOLOCK)
    ON TE.IdSeccionSmart = AC.IdSeccion
WHERE AC.IdSeccion = {0}
  AND AC.EsMatricula = 1
  AND TE.EstadoTeam = 'A'
  AND NULLIF(AL.EmailInstitucion, '') IS NOT NULL
  AND NULLIF(COALESCE(NULLIF(AL.Codigo, ''), CONVERT(VARCHAR(50), AL.IdAlumno)), '') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM TeamsUsuarios TU WITH (NOLOCK)
      WHERE TU.IdTeams = TE.IdTeamsGroup
        AND TU.CodigoAlumno = COALESCE(NULLIF(AL.Codigo, ''), CONVERT(VARCHAR(50), AL.IdAlumno))
        AND TU.Tipo = 'A'
        AND TU.Estado = 'A'
  );";

            return await _context.Database.SqlQueryRaw<MissingStudentDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

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
            var sql = @"
WITH EnrolledIdentifiers AS (
    SELECT DISTINCT
        IdSeccion = AC.IdSeccion,
        Identifier = LOWER(LTRIM(RTRIM(COALESCE(NULLIF(AL.Codigo, ''), CONVERT(VARCHAR(50), AL.IdAlumno)))))
    FROM AlumnoCurso AC WITH (NOLOCK)
    INNER JOIN Alumno AL WITH (NOLOCK)
        ON AL.IdAlumno = AC.IdAlumno
    WHERE AC.IdSeccion = {0}
      AND AC.EsMatricula = 1

    UNION

    SELECT DISTINCT
        IdSeccion = AC.IdSeccion,
        Identifier = LOWER(LTRIM(RTRIM(LEFT(AL.EmailInstitucion, CHARINDEX('@', AL.EmailInstitucion + '@') - 1))))
    FROM AlumnoCurso AC WITH (NOLOCK)
    INNER JOIN Alumno AL WITH (NOLOCK)
        ON AL.IdAlumno = AC.IdAlumno
    WHERE AC.IdSeccion = {0}
      AND AC.EsMatricula = 1
      AND ISNULL(AL.EmailInstitucion, '') <> ''
),
dtOldMembers AS (
    SELECT
        TE.IdTeamsGroup,
        TU.CodigoAlumno,
        TU.Email AS EmailAlumno
    FROM TeamsUsuarios TU WITH (NOLOCK)
    INNER JOIN TeamsEquipos TE WITH (NOLOCK)
        ON TE.IdTeamsGroup = TU.IdTeams
    WHERE TE.IdSeccionSmart = {0}
      AND TE.EstadoTeam = 'A'
      AND TU.Estado = 'A'
      AND TU.Tipo = 'A'
      AND NOT EXISTS
      (
          SELECT 1
          FROM EnrolledIdentifiers EI
          WHERE EI.IdSeccion = TE.IdSeccionSmart
            AND EI.Identifier = LOWER(LTRIM(RTRIM(COALESCE(
                NULLIF(TU.CodigoAlumno, ''),
                LEFT(ISNULL(TU.Email, ''), CHARINDEX('@', ISNULL(TU.Email, '') + '@') - 1)
            ))))
      )
)
SELECT *
FROM dtOldMembers
WHERE ISNULL(IdTeamsGroup, '') <> '';";

            return await _context.Database.SqlQueryRaw<ObsoleteStudentDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

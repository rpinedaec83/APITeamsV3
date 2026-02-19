using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncRenamedTeamsCommandHandler : IRequestHandler<SyncRenamedTeamsCommand, List<RenamedTeamDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncRenamedTeamsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<RenamedTeamDto>> Handle(SyncRenamedTeamsCommand request, CancellationToken cancellationToken)
        {
            // Option 7 Logic: Find teams where the name or description has changed
            // compared to the expected naming convention from Smart data.
            // Returns the list so the caller can update the Teams display name via Graph API.

            var sql = @"
                WITH dtDetailTeam AS (
                  SELECT TE.IdSeccionSmart,
                    TE.NombreTeam,
                    TE.DescripcionTeam
                  FROM TeamsEquipos TE WITH (NOLOCK)
                  WHERE TE.IdSeccionSmart = {0}
                  EXCEPT
                  SELECT DISTINCT M.IdCurso,
                    NombreCurso + ' [' + M.NombreProducto + '][' + S.Codigo + ']' AS Nombre,
                    'Descripcion' = CASE
                      WHEN LEN(
                        'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador
                      ) >= 250 THEN SUBSTRING(
                        'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador,
                        1,
                        250
                      )
                      ELSE 'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador
                    END
                  FROM TeamsProgramacionGeneral M WITH (NOLOCK)
                    LEFT JOIN EmpresaSedeParametro ES WITH (NOLOCK) ON (ES.IdSede = M.IdSede)
                    LEFT JOIN Seccion S WITH (NOLOCK) ON (S.IdSeccion = M.IdCurso)
                  WHERE ES.Nombre = 'PROPIETARIOTINA'
                    AND ISNULL(S.IdSeccion, '') <> ''
                )
                SELECT TE.IdTeamsGroup,
                  DT.NombreTeam,
                  DT.DescripcionTeam
                FROM dtDetailTeam DT WITH (NOLOCK)
                  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (DT.IdSeccionSmart = TE.IdSeccionSmart)
                WHERE ISNULL(TE.IdTeamsGroup, '') <> ''
                  AND TE.EstadoTeam = 'A'";

            return await _context.Database.SqlQueryRaw<RenamedTeamDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncTeamFacilitatorsCommandHandler : IRequestHandler<SyncTeamFacilitatorsCommand, List<TeamFacilitatorChangeDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncTeamFacilitatorsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<TeamFacilitatorChangeDto>> Handle(SyncTeamFacilitatorsCommand request, CancellationToken cancellationToken)
        {
            // Option 2 Logic: SYNC FACILITATORS
            // 1. Calculate Propietario4 (Owner)
            // 2. Update TeamsEquipos (Set P3=NULL, P4=NewOwner)
            // 3. Return affected teams

            var sql = @"
                DECLARE @propietario4 VARCHAR(200);

                SELECT @propietario4 = ES.Valor
                FROM TeamsProgramacionGeneral M WITH(NOLOCK)
                  LEFT JOIN EmpresaSedeParametro ES WITH(NOLOCK) ON (
                    ES.IdSede = M.IdSede
                    AND ES.Nombre = 'PROPIETARIOTINA'
                    AND M.IdUnidadNegocio = convert(INT, ES.Valor3)
                  )
                WHERE M.IdCurso = {0};

                UPDATE TeamsEquipos
                SET Propietario3 = NULL,
                    Propietario4 = @propietario4
                WHERE Propietario4 IS NULL
                  AND IdSeccionSmart = {0};

                WITH dtFacilitadores AS (
                  SELECT DISTINCT TE.IdTeamsGroup AS IdTeam,
                    MPG.EmailFacilitador,
                    MPG.CodigoFacilitador,
                    MPG.NombresFacilitador,
                    MPG.ApellidosFacilitador
                  FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)
                    LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)
                  WHERE TE.IdSeccionSmart = {0}
                    AND NOT EXISTS (
                      SELECT 1
                      FROM TeamsEquipos T WITH (NOLOCK)
                      WHERE T.IdSeccionSmart = MPG.IdCurso
                        AND Propietario3 = MPG.EmailFacilitador
                        AND EstadoTeam = 'A'
                    )
                    AND TE.EstadoTeam = 'A'
                )
                SELECT F.IdTeam,
                  F.CodigoFacilitador,
                  F.NombresFacilitador,
                  F.ApellidosFacilitador,
                  CASE
                    WHEN ISNULL(TU.Propietario3, '') = '' THEN TU.Propietario3
                    ELSE SUBSTRING(TU.Propietario3, 1, CHARINDEX('@', TU.Propietario3) - 1)
                  END AS OldCodigoFacilitador
                FROM dtFacilitadores F
                  LEFT JOIN TeamsEquipos TU WITH (NOLOCK) ON (TU.IdTeamsGroup = F.IdTeam)
                WHERE ISNULL(IdTeam, '') <> ''
                  AND TU.Propietario3 IS NOT NULL;";

            return await _context.Database.SqlQueryRaw<TeamFacilitatorChangeDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

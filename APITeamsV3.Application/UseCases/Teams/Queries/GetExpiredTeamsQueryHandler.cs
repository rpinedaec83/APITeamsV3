using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetExpiredTeamsQueryHandler : IRequestHandler<GetExpiredTeamsQuery, List<ExpiredTeamDto>>
    {
        private readonly ISmartDbContext _context;

        public GetExpiredTeamsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExpiredTeamDto>> Handle(GetExpiredTeamsQuery request, CancellationToken cancellationToken)
        {
            // Option 6 Logic: TRAE LOS TEAMS A ELIMINAR
            var sql = @"
                DECLARE @FechaIniDias INT = 14,
                        @FechaFinDias INT = 14
                SELECT @FechaIniDias = CONVERT(INT, Valor),
                       @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK)
                WHERE Nombre = 'EsTeams'

                SELECT TE.IdTeamsGroup
                FROM TeamsEquipos TE WITH (NOLOCK)
                  INNER JOIN Seccion se WITH (NOLOCK) ON te.IdSeccionSmart = SE.IdSeccion
                  INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso
                  INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                  INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
                  AND SE.IdGrupo = PG.IdGrupo
                  INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo
                WHERE TE.IdSeccionSmart = {0}
                  AND (
                    (
                      (
                        PR.TipoServicio = 'P'
                        OR PR.TipoServicio = 'L'
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaFinDias, se.FechaFin),
                        112
                      )
                    )
                    OR (
                      PR.TipoServicio = 'C'
                      AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaFinDias, PE.Fin),
                        112
                      )
                    )
                  )
                  AND NOT EXISTS (
                    SELECT 1
                    FROM TeamsProgramacionGeneral TPG WITH(NOLOCK)
                    WHERE TPG.IdCurso = TE.IdSeccionSmart
                      AND EstadoTeam = 'A'
                  )
                  AND EstadoTeam = 'A'
                UNION
                select TE.IdTeamsGroup
                from TeamsEquipos TE WITH (NOLOCK)
                where TE.IdSeccionSmart not in (
                    select IdSeccion
                    from Seccion WITH (NOLOCK)
                    where IdSeccion = {0}
                  )
                  and TE.EstadoTeam = 'A'
                  and TE.IdSeccionSmart = {0}
                  AND (
                    left(NombreTeam, 20) like '%2020%'
                    OR left(NombreTeam, 20) like '%2021%'
                    OR left(NombreTeam, 20) like '%2022%'
                    OR left(NombreTeam, 20) like '%2023%'
                  )";

            return await _context.Database.SqlQueryRaw<ExpiredTeamDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

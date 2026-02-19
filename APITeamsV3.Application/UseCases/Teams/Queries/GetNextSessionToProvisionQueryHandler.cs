using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetNextSessionToProvisionQueryHandler : IRequestHandler<GetNextSessionToProvisionQuery, NextSessionDto?>
    {
        private readonly ISmartDbContext _context;

        public GetNextSessionToProvisionQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<NextSessionDto?> Handle(GetNextSessionToProvisionQuery request, CancellationToken cancellationToken)
        {
            // 1. Get Parameters (replicating SP logic)
            // @FechaIniDias, @FechaFinDias from Parametro where Nombre = 'EsTeams'
            var esTeamsParam = await _context.Set<APITeamsV3.Domain.Entities.Parametro>()
                .Where(p => p.Nombre == "EsTeams")
                .Select(p => p.Valor)
                .FirstOrDefaultAsync(cancellationToken);

            int fechaIniDias = 14;
            int fechaFinDias = 14;

            if (!string.IsNullOrEmpty(esTeamsParam) && int.TryParse(esTeamsParam, out int val))
            {
                fechaIniDias = val;
                fechaFinDias = val; // SP logic: @FechaFinDias = CONVERT(INT, Valor) from same row? Yes, line 67
            }

            // 2. Execute SQL
            // Adapted from cTeamsPorSeccion @Opcion = 16
            var sql = @"
WITH dtEvents AS (
  SELECT DISTINCT HS.IdSeccion,
    HS.IdHorario,
    HS.Numero,
    S.Codigo
  FROM HorarioSesion HS WITH (NOLOCK)
    INNER JOIN Seccion S WITH (NOLOCK) ON HS.IdSeccion = S.IdSeccion
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = HS.IdSeccion
    INNER JOIN TeamsUsuarios TU WITH (NOLOCK) ON TU.idTeams = TE.IdTeamsGroup
  WHERE HS.idseccion = {0}
    AND ISNULL(TU.CodigoAlumno, '') <> ''
    AND TU.Tipo = 'A'
    AND NOT EXISTS (
      SELECT top 1 1
      FROM SeccionHorario TH WITH (NOLOCK)
      WHERE TH.idseccion = {1}
        and TH.UrlClaseVirtual <> ''
    )
)
SELECT top 1 TE.IdTeamsGroup,
  TE.Propietario2 AS EmailAppTeam,
  HS.IdHorario,
  S.Codigo,
  E.Numero,
  HS.Fecha,
  HS.Inicio,
  HS.Fin,
  MPG.IdCurso,
  A.NombreCompleto,
  FA.CodigoAnterior,
  FA.EmailInstitucion,
  MPG.NombresFacilitador,
  MPG.EmailFacilitador,
  MPG.Resumen AS Content,
  TE.NombreTeam AS SubjectMeet,
  TE.DescripcionTeam,
  TE.MailNickName,
  TE.IsActive,
  UN.IdUnidadNegocio,
  PE1.Valor2 AS CuentaCarreras,
  PE2.Valor4 AS CuentaExtension
FROM dtEvents E WITH (NOLOCK)
  INNER JOIN HorarioSesion HS WITH (NOLOCK) ON (
    HS.IdSeccion = E.IdSeccion
    AND HS.IdHorario = E.IdHorario
    AND HS.Estado <> 'X'
  )
  INNER JOIN Seccion S WITH (NOLOCK) ON S.IdSeccion = E.IdSeccion
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = E.IdSeccion
  INNER JOIN Actor A WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = A.IdActor
  INNER JOIN Facilitador FA WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = FA.IdFacilitador
  INNER JOIN TeamsProgramacionAlumnos MPG WITH (NOLOCK) ON (MPG.IdCurso = S.IdSeccion)
  INNER JOIN Promocion PR WITH (NOLOCK) ON S.IdPromocion = PR.IdPromocion
  INNER JOIN PromocionGrupo PG on PG.IdPromocion = S.IdPromocion
  and PG.IdGrupo = S.IdGrupo
  INNER JOIN Periodo PE on PR.IdPeriodo = PE.IdPeriodo
  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
  LEFT JOIN ParametroEmpresa PE1 WITH (NOLOCK) ON UN.IdUnidadNegocio = CONVERT(int, PE1.Valor)
  and PE1.Nombre = 'TEAMSMEET'
  LEFT JOIN ParametroEmpresa PE2 WITH (NOLOCK) ON UN.IdUnidadNegocio = CONVERT(int, PE2.Valor3)
  and PE2.Nombre = 'TEAMSMEET'
WHERE TE.EstadoTeam = 'A'
  and PG.Estado = 'A'
  AND CONVERT(VARCHAR, GETDATE(), 112) >= CASE
    WHEN un.TipoServicio = 'C' THEN CONVERT(VARCHAR, dateadd(D, -{2}, PE.Inicio), 112)
    ELSE CONVERT(
      VARCHAR,
      dateadd(D, -{3}, S.FechaInicio),
      112
    )
  END
  AND CONVERT(VARCHAR, GETDATE(), 112) <= CASE
    WHEN un.TipoServicio = 'C' THEN CONVERT(VARCHAR, dateadd(D, {4}, PE.Fin), 112)
    ELSE CONVERT(VARCHAR, dateadd(D, {5}, S.FechaFin), 112)
  END
ORDER BY hs.Fecha ASC";

            // We use Database.SqlQueryRaw or FromSqlRaw.
            // Since NextSessionDto is NOT an entity, we must use SqlQuery (EF Core 8 feature) or a wrapper.
            // For older EF Core, we might need a keyless entity.
            // Assuming EF Core 8+ based on context.
            
            // Note: EF Core 8 SqlQuery returns IQueryable<T> for scalar/non-entity types.
            // We pass parameters by index.
            
            var result = await _context.Database.SqlQueryRaw<NextSessionDto>(
                sql, 
                request.IdSeccion, // {0}
                request.IdSeccion, // {1}
                fechaIniDias,      // {2}
                fechaIniDias,      // {3}
                fechaFinDias,      // {4}
                fechaFinDias       // {5}
            ).FirstOrDefaultAsync(cancellationToken);

            return result;
        }
    }
}

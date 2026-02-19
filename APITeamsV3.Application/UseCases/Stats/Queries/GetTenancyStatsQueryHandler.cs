using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetTenancyStatsQueryHandler : IRequestHandler<GetTenancyStatsQuery, List<TenancyStatsDto>>
    {
        private readonly ISmartDbContext _context;

        public GetTenancyStatsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<TenancyStatsDto>> Handle(GetTenancyStatsQuery request, CancellationToken cancellationToken)
        {
            // Option 36 Logic: Optimized with CTEs
            var sql = @"
                DECLARE @FechaIniDias INT = 14,
                        @FechaFinDias INT = 14;
                SELECT @FechaIniDias = CONVERT(INT, Valor),
                       @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK)
                WHERE Nombre = 'EsTeams';

                WITH BaseSecciones AS (
                    SELECT 
                        PE.IdPeriodo, 
                        PE.Codigo AS Periodo,
                        SD.IdSede,
                        SD.Codigo AS Sede,
                        UN.Nombre AS UnidadNegocio,
                        UA.Nombre AS Programa,
                        SE.IdSeccion, 
                        TE.IdTeamsGroup,
                        TE.IsActive,
                        TE.Propietario3
                    FROM Seccion SE WITH (NOLOCK)
                        INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                        INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                        INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                        INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                        INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                        INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto
                        LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON SE.IdSeccion = TE.IdSeccionSmart AND TE.EstadoTeam = 'A'
                    WHERE PE.EsTeams = 1
                      AND (
                        (
                          (PR.TipoServicio = 'P' OR PR.TipoServicio = 'L')
                          AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)
                          AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, SE.FechaFin), 112)
                        )
                        OR (
                          PR.TipoServicio = 'C'
                          AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)
                          AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, PE.Fin), 112)
                        )
                      )
                ),
                TeamStats AS (
                    SELECT 
                        IdPeriodo, Sede, UnidadNegocio, Programa, Periodo,
                        COUNT(IdSeccion) AS Equipos,
                        SUM(CASE WHEN IsActive = 'A' THEN 1 ELSE 0 END) AS EquiposActivos,
                        SUM(CASE WHEN Propietario3 IS NOT NULL THEN 1 ELSE 0 END) AS Docentes,
                        SUM(CASE WHEN Propietario3 IS NULL THEN 1 ELSE 0 END) AS NoDocentes
                    FROM BaseSecciones
                    GROUP BY IdPeriodo, Sede, UnidadNegocio, Programa, Periodo
                ),
                StudentData AS (
                    SELECT 
                        BS.IdPeriodo, BS.Sede, BS.UnidadNegocio, BS.Programa,
                        A.CodigoAnterior,
                        TU.CodigoAlumno AS TeamsUser
                    FROM BaseSecciones BS
                        INNER JOIN AlumnoCurso AC WITH (NOLOCK) ON BS.IdSeccion = AC.IdSeccion AND AC.EsMatricula = 1
                        INNER JOIN Alumno A WITH (NOLOCK) ON AC.IdAlumno = A.IdAlumno
                        LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON BS.IdTeamsGroup = TU.IdTeams 
                            AND A.CodigoAnterior = TU.CodigoAlumno AND TU.Tipo = 'A'
                ),
                StudentStats AS (
                    SELECT 
                        IdPeriodo, Sede, UnidadNegocio, Programa,
                        COUNT(CodigoAnterior) AS CursoxAlumnos,
                        COUNT(TeamsUser) AS CursoxTeams,
                        COUNT(DISTINCT CodigoAnterior) AS Alumnos,
                        COUNT(DISTINCT TeamsUser) AS EnTeams
                    FROM StudentData
                    GROUP BY IdPeriodo, Sede, UnidadNegocio, Programa
                )
                SELECT 
                    TS.IdPeriodo, TS.Sede, TS.Periodo, TS.UnidadNegocio, TS.Programa,
                    TS.Equipos,
                    TS.EquiposActivos,
                    CAST(CASE WHEN TS.Equipos = 0 THEN 0 ELSE (CAST(TS.EquiposActivos AS DECIMAL) / TS.Equipos) END AS DECIMAL(5,2)) AS PorEquiposActivos,
                    TS.Docentes,
                    TS.NoDocentes,
                    CAST(CASE WHEN TS.Equipos = 0 THEN 0 ELSE (CAST(TS.Docentes AS DECIMAL) / TS.Equipos) END AS DECIMAL(5,2)) AS PorDocente,
                    ISNULL(SS.CursoxAlumnos, 0) AS CursoxAlumnos,
                    ISNULL(SS.CursoxTeams, 0) AS CursoxTeams,
                    CAST(CASE WHEN ISNULL(SS.CursoxAlumnos, 0) = 0 THEN 0 ELSE (CAST(ISNULL(SS.CursoxTeams, 0) AS DECIMAL) / SS.CursoxAlumnos) END AS DECIMAL(5,2)) AS PorCursoxAlumnos,
                    ISNULL(SS.Alumnos, 0) AS Alumnos,
                    ISNULL(SS.EnTeams, 0) AS EnTeams,
                    CAST(CASE WHEN ISNULL(SS.Alumnos, 0) = 0 THEN 0 ELSE (CAST(ISNULL(SS.EnTeams, 0) AS DECIMAL) / SS.Alumnos) END AS DECIMAL(5,2)) AS PorAlumnos
                FROM TeamStats TS
                    LEFT JOIN StudentStats SS ON TS.IdPeriodo = SS.IdPeriodo 
                        AND TS.Sede = SS.Sede 
                        AND TS.UnidadNegocio = SS.UnidadNegocio 
                        AND TS.Programa = SS.Programa
                ORDER BY TS.Periodo, TS.Sede, TS.UnidadNegocio, TS.Programa";

            return await _context.Database.SqlQueryRaw<TenancyStatsDto>(sql).ToListAsync(cancellationToken);
        }
    }
}

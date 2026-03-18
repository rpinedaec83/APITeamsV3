using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetAllSectionsToSyncQueryHandler : IRequestHandler<GetAllSectionsToSyncQuery, List<int>>
    {
        private readonly ISmartDbContext _context;

        public GetAllSectionsToSyncQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> Handle(GetAllSectionsToSyncQuery request, CancellationToken cancellationToken)
        {
            // Option 19 Logic: TRAE TODOS LOS CURSOS DE SMART
            // Returns all section IDs that need synchronization, combining:
            //   1. Active Smart sections filtered by Sede + Periodo (EsTeams=1, date range)
            //   2. Active TeamsEquipos entries for those sedes
            //   3. Orphan TeamsEquipos (no matching Seccion record)
            var sql = @"
                DECLARE @FechaIniDias INT = 14,
                        @FechaFinDias INT = 14

                SELECT @FechaIniDias = CONVERT(INT, Valor),
                       @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK)
                WHERE Nombre = 'EsTeams'

                DECLARE @Empresa VARCHAR(8);
                SELECT TOP 1 @Empresa = CompaniaSocio
                FROM Empresa WITH(NOLOCK)
                WHERE IdEmpresa = 1

                DECLARE @CodigoSede VARCHAR(2)
                SELECT @CodigoSede = CASE
                    WHEN @Empresa = '00002700' THEN 'SV'
                    WHEN @Empresa = '00002600' THEN 'VT'
                    WHEN @Empresa = '00002500' THEN 'VI'
                END

                DECLARE @SedeTMP TABLE (IdSede INT)

                IF {0} IS NULL
                BEGIN
                    INSERT INTO @SedeTMP
                    SELECT IdSede
                    FROM Sede WITH(NOLOCK)
                    WHERE Codigo = @CodigoSede
                END
                ELSE
                BEGIN
                    INSERT INTO @SedeTMP
                    SELECT IdSede
                    FROM Sede WITH(NOLOCK)
                    WHERE Codigo IN (
                        SELECT value
                        FROM STRING_SPLIT({0}, ',')
                    );
                END

                DECLARE @tmp AS TABLE (IdCurso INT)

                INSERT INTO @tmp
                SELECT DISTINCT SE.IdSeccion
                FROM Seccion SE WITH (NOLOCK)
                    INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa
                    INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                    INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad
                    INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                    INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                    INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                    INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
                    LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo
                        AND PR.IdCurricula = CM.IdCurricula
                    LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro
                    LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
                        AND SE.IdGrupo = PG.IdGrupo
                    LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
                    LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente
                    LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor
                    LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno
                    LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = MTR2.IdMaestroRegistro
                    LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
                        AND (SP.EsResponsable = 1)
                    LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
                    LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
                WHERE PE.EsTeams = 1
                    AND (
                        (
                            (PR.TipoServicio = 'P' OR PR.TipoServicio = 'L')
                            AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, SE.FechaInicio), 112)
                                AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, SE.FechaFin), 112)
                        )
                        OR (
                            PR.TipoServicio = 'C'
                            AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, PE.Inicio), 112)
                                AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, PE.Fin), 112)
                        )
                    )
                    AND SD.IdSede IN (
                        SELECT IdSede
                        FROM @SedeTMP
                    )

                INSERT INTO @tmp
                SELECT IdSeccionSmart
                FROM TeamsEquipos TE WITH (NOLOCK)
                    INNER JOIN Seccion SE WITH (NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion
                    INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa
                    INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                WHERE EstadoTeam = 'A'
                    AND SD.IdSede IN (
                        SELECT IdSede
                        FROM @SedeTMP
                    )
                UNION
                SELECT TE.IdSeccionSmart
                FROM TeamsEquipos TE WITH (NOLOCK)
                    LEFT JOIN Seccion S WITH (NOLOCK) ON S.IdSeccion = TE.IdSeccionSmart
                WHERE TE.EstadoTeam = 'A'
                    AND S.IdSeccion IS NULL

                SELECT DISTINCT IdCurso FROM @tmp";

            var results = await _context.Database
                .SqlQueryRaw<SectionIdDto>(sql, request.Sede)
                .ToListAsync(cancellationToken);

            return results.Select(r => r.IdCurso).ToList();
        }
    }
}

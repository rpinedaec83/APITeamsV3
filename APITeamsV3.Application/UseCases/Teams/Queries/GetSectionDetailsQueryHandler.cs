using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetSectionDetailsQueryHandler : IRequestHandler<GetSectionDetailsQuery, List<SectionDetailsDto>>
    {
        private readonly ISmartDbContext _context;

        public GetSectionDetailsQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<SectionDetailsDto>> Handle(GetSectionDetailsQuery request, CancellationToken cancellationToken)
        {
            // Option 24 Logic
            var sql = @"
                DECLARE @FechaIniDias INT = 14,
                        @FechaFinDias INT = 14
                SELECT @FechaIniDias = CONVERT(INT, Valor),
                       @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK)
                WHERE Nombre = 'EsTeams'

                SELECT 'Sede' = SD.Nombre,
                  'Division' = UN.Nombre,
                  'Programa' = UA.Nombre,
                  'Periodo' = Pe.Codigo,
                  'Producto' = PO.ProductoNombre,
                  PR.PromocionCodigo,
                  'Seccion' = PG.GrupoCodigo,
                  'CursoCodigo' = SE.Codigo,
                  'CursoNombre' = cu.CursoNombre,
                  'EstadoCursoHorario' = se.Estado,
                  'Inicio' = CONVERT(VARCHAR, pg.FechaInicio, 104),
                  'Fin' = CONVERT(VARCHAR, pg.FechaFin, 104),
                  'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, ''),
                  'FacilitadorNombre' = ISNULL(AR.NombreCompleto, ''),
                  'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion),
                  'TotalAlumnos' = COUNT(AC.IdAlumno),
                  'IdTeamsGroup' = te.IdTeamsGroup,
                  te.NombreTeam,
                  SE.IdSeccion
                FROM Seccion SE WITH (NOLOCK)
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion
                  AND SP.EsResponsable = 1
                  LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador
                  LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor
                  INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                  INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
                  AND SE.IdGrupo = PG.IdGrupo
                  INNER JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
                  INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                  INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                  INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                  INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto
                  LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion
                  AND AC.EsMatricula = 1
                  INNER JOIN Matricula M ON AC.IdMatricula = M.IdMatricula
                  AND M.EsMatricula = 1 
                  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON SE.IdSeccion = TE.IdSeccionSmart
                WHERE PE.EsTeams = 1
                  AND (
                    (
                      (
                        PR.TipoServicio = 'P'
                        OR PR.TipoServicio = 'L'
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaIniDias * - 1, SE.FechaInicio),
                        112
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaFinDias, se.FechaFin),
                        112
                      )
                    )
                    OR (
                      PR.TipoServicio = 'C'
                      AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaIniDias * - 1, PE.Inicio),
                        112
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(
                        VARCHAR,
                        DateADD(DAY, @FechaFinDias, PE.Fin),
                        112
                      )
                    )
                  )
                  AND ISNULL({0}, SE.IdSeccion) = SE.IdSeccion
                GROUP BY SD.Nombre,
                  UN.Nombre,
                  UA.Nombre,
                  Pe.Codigo,
                  PO.ProductoNombre,
                  PR.PromocionCodigo,
                  PG.GrupoCodigo,
                  SE.Codigo,
                  cu.CursoNombre,
                  se.Estado,
                  CONVERT(VARCHAR, pg.FechaInicio, 104),
                  CONVERT(VARCHAR, pg.FechaFin, 104),
                  ISNULL(FA.CodigoAnterior, ''),
                  ISNULL(AR.NombreCompleto, ''),
                  dbo.gFrecuenciaSeccionHorario(SE.IdSeccion),
                  te.IdTeamsGroup,
                  te.NombreTeam,
                  se.IdSeccion
                ORDER BY SD.Nombre,
                  UN.Nombre,
                  UA.Nombre,
                  Pe.Codigo,
                  PO.ProductoNombre,
                  PR.PromocionCodigo,
                  PG.GrupoCodigo,
                  CU.CursoNombre";

            // Note: I replaced the Date filtering logic from BETWEEN to separate >= and <= for clarity in raw string,
            // but kept the logic identical to existing patterns.
            // Also checking if I missed any params. The original SQL uses @IdSeccion.

            return await _context.Database.SqlQueryRaw<SectionDetailsDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

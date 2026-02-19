using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetStudentSyncStatusQueryHandler : IRequestHandler<GetStudentSyncStatusQuery, List<StudentSyncStatusDto>>
    {
        private readonly ISmartDbContext _context;

        public GetStudentSyncStatusQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<StudentSyncStatusDto>> Handle(GetStudentSyncStatusQuery request, CancellationToken cancellationToken)
        {
            // Option 35 Logic
            var sql = @"
                DECLARE @FechaIniDias INT = 14,
                        @FechaFinDias INT = 14
                SELECT @FechaIniDias = CONVERT(INT, Valor),
                       @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK)
                WHERE Nombre = 'EsTeams'

                SELECT DISTINCT 'IdCurso' = SE.IdSeccion,
                  'Sede' = SD.Nombre,
                  'Division' = UN.Nombre,
                  'Programa' = UA.Nombre,
                  'Periodo' = Pe.Codigo,
                  'Producto' = PO.ProductoNombre,
                  PR.PromocionCodigo,
                  'Seccion' = PG.GrupoCodigo,
                  'CursoCodigo' = SE.Codigo,
                  'CursoNombre' = cu.CursoNombre,
                  'EstadoCursoHorario' = se.Estado,
                  'InicioPeriodo' = CONVERT(VARCHAR, pg.FechaInicio, 104),
                  'FinPeriodo' = CONVERT(VARCHAR, pg.FechaFin, 104),
                  'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, '-SIN FACILITADOR-'),
                  'FacilitadorNombre' = ISNULL(AR.NombreCompleto, '-SIN FACILITADOR-'),
                  'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion),
                  te.NombreTeam,
                  te.Propietario2,
                  a.CodigoAnterior,
                  'NombreCompleto' = act.NombreCompleto,
                  ISNULL(tu.CodigoAlumno, 'No Sincronizado') AS CodigoTeams,
                  ac.FechaModificacion,
                  u.Nombres,
                  SE.IdSeccion,
                  te.IdTeamsGroup,
                  SE.FechaInicio
                FROM AlumnoCurso AC WITH (NOLOCK)
                  LEFT JOIN Matricula M ON AC.IdMatricula = M.IdMatricula
                  AND M.EsMatricula = 1 
                  LEFT JOIN usuario U ON ac.UsuarioModificacion = U.IdUsuario
                  INNER JOIN Alumno a WITH (NOLOCK) ON a.IdAlumno = ac.IdAlumno
                  INNER JOIN actor act WITH (NOLOCK) ON a.IdAlumno = act.IdActor
                  INNER JOIN Seccion SE WITH (NOLOCK) ON AC.IdSeccion = SE.IdSeccion
                  INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso
                  INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                  INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
                  AND SE.IdGrupo = PG.IdGrupo
                  INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo
                  INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede
                  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                  INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                  INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion
                  AND SP.EsResponsable = 1
                  LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador
                  LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor
                  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON te.IdSeccionSmart = se.IdSeccion
                  LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams
                  AND a.CodigoAnterior = tu.CodigoAlumno
                  AND tu.Tipo = 'A'
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
                  AND aC.EsMatricula = 1
                  AND m.EsMatricula = 1
                  AND TE.EstadoTeam = 'A'";

            return await _context.Database.SqlQueryRaw<StudentSyncStatusDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

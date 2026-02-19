using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class GenerateSectionScheduleCommandHandler : IRequestHandler<GenerateSectionScheduleCommand, bool>
    {
        private readonly ISmartDbContext _context;

        public GenerateSectionScheduleCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(GenerateSectionScheduleCommand request, CancellationToken cancellationToken)
        {
            // 1. Get Parameters
            var esTeamsParam = await _context.Set<APITeamsV3.Domain.Entities.Parametro>()
                .Where(p => p.Nombre == "EsTeams")
                .Select(p => p.Valor)
                .FirstOrDefaultAsync(cancellationToken);

            int fechaIniDias = 14;
            int fechaFinDias = 14;

            if (!string.IsNullOrEmpty(esTeamsParam) && int.TryParse(esTeamsParam, out int val))
            {
                fechaIniDias = val;
                fechaFinDias = val; 
            }

            // 2. Execute Deletes (Option 0/1 Logic)
            var deleteSql = @"
                DELETE TeamsProgramacionGeneral WHERE IdCurso = {0};
                DELETE TeamsProgramacionAlumnos WHERE IdCurso = {0};
            ";
            
            await _context.Database.ExecuteSqlRawAsync(deleteSql, request.IdSeccion);

            // 3. Execute Inserts (Option 0/1 Logic)
            // Note: Using concatenated SQL to replicate the exact legacy logic.
            // Parameters: {0} = IdSeccion, {1} = FechaIniDias, {2} = FechaFinDias
            var insertGeneralSql = @"
                INSERT INTO TeamsProgramacionGeneral
                SELECT DISTINCT SD.IdSede,
                  'NombreSede' = SD.Nombre,
                  FA.IdFacultad,
                  'NombreFacultad' = FA.Nombre,
                  UN.IdUnidadNegocio,
                  'NombreUnidadNegocio' = UN.Nombre,
                  UA.IdUnidadAcademica,
                  'NombreUnidadAcademica' = UA.Nombre,
                  PE.IdPeriodo,
                  'CodigoPeriodo' = PE.Codigo,
                  PD.IdProducto,
                  'NombreProducto' = PD.ProductoNombre,
                  pr.IdPromocion,
                  'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0'),
                  PG.IdGrupo,
                  PG.GrupoCodigo,
                  'IdCurso' = SE.IdSeccion,
                  'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) 
                ,
                  'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre,
                  'Resumen' = CU.CursoNombre,
                  'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, ''),
                  'NombresFacilitador' = ISNULL(
                    REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''),
                    ''
                  ) + ' ' + ISNULL(
                    REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''),
                    ''
                  ) + ' ' + ISNULL(
                    REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''),
                    ''
                  ),
                  'EmailFacilitador' = ISNULL(FC.EmailInstitucion, ''),
                  1,
                  1,
                  GETDATE(),
                  NULL
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
                  LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
                  AND (SP.EsResponsable = 1) 
                  LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
                  LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
                WHERE PE.EsTeams = 1
                  AND (
                    (
                      (
                        PR.TipoServicio = 'P'
                        OR PR.TipoServicio = 'L'
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(
                        VARCHAR,
                        DATEADD(DAY, {1} * - 1, SE.FechaInicio),
                        112
                      )
                      AND CONVERT(
                        VARCHAR,
                        DateADD(DAY, {2}, se.FechaFin),
                        112
                      )
                    )
                    OR (
                      PR.TipoServicio = 'C'
                      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(
                        VARCHAR,
                        DATEADD(DAY, {1} * - 1, PE.Inicio),
                        112
                      )
                      AND CONVERT(
                        VARCHAR,
                        DateADD(DAY, {2}, PE.Fin),
                        112
                      )
                    )
                  )
                  AND SE.IdSeccion = {0};
            ";
            
            await _context.Database.ExecuteSqlRawAsync(insertGeneralSql, request.IdSeccion, fechaIniDias, fechaFinDias);

            var insertAlumnosSql = @"
                INSERT INTO TeamsProgramacionAlumnos
                SELECT DISTINCT SD.IdSede,
                  'NombreSede' = SD.Nombre,
                  FA.IdFacultad,
                  'NombreFacultad' = FA.Nombre,
                  UN.IdUnidadNegocio,
                  'NombreUnidadNegocio' = UN.Nombre,
                  UA.IdUnidadAcademica,
                  'NombreUnidadAcademica' = UA.Nombre,
                  PE.IdPeriodo,
                  'CodigoPeriodo' = PE.Codigo,
                  PD.IdProducto,
                  'NombreProducto' = PD.ProductoNombre,
                  pr.IdPromocion,
                  'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0'),
                  PG.IdGrupo,
                  PG.GrupoCodigo,
                  'IdCurso' = SE.IdSeccion,
                  'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) 
                ,
                  'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre,
                  'Resumen' = CU.CursoNombre,
                  'CodigoAlumno' = AL.CodigoAnterior,
                  'NombresAlumno' = REPLACE(REPLACE(AT.Nombres, 'Ñ', 'N'), '''', ''),
                  'ApellidosAlumno' = REPLACE(REPLACE(AT.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(
                    REPLACE(REPLACE(AT.Materno, 'Ñ', 'N'), '''', ''),
                    ''
                  ),
                  'EmailAlumno' = AL.EmailInstitucion,
                  'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, ''),
                  'NombresFacilitador' = ISNULL(
                    REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''),
                    ''
                  ) + ' ' + ISNULL(
                    REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''),
                    ''
                  ) + ' ' + ISNULL(
                    REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''),
                    ''
                  ),
                  'EmailFacilitador' = ISNULL(FC.EmailInstitucion, ''),
                  'Estado' = AC.estado,
                  1,
                  1,
                  GETDATE(),
                  NULL
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
                  LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro
                  LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion
                  LEFT JOIN Matricula M WITH (NOLOCK) ON M.IdMatricula = AC.IdMatricula
                  AND M.EsMatricula = 1 
                  LEFT JOIN Alumno AL WITH (NOLOCK) ON AC.IdAlumno = AL.IdAlumno
                  LEFT JOIN Actor AT WITH (NOLOCK) ON AC.IdAlumno = AT.IdActor
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
                  AND (SP.EsResponsable = 1) 
                  LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
                  LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
                WHERE PE.EsTeams = 1
                  AND (
                    (
                      (
                        PR.TipoServicio = 'P'
                        OR PR.TipoServicio = 'L'
                      )
                      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(
                        VARCHAR,
                        DATEADD(DAY, {1} * - 1, SE.FechaInicio),
                        112
                      )
                      AND CONVERT(
                        VARCHAR,
                        DateADD(DAY, {2}, se.FechaFin),
                        112
                      )
                    )
                    OR (
                      PR.TipoServicio = 'C'
                      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(
                        VARCHAR,
                        DATEADD(DAY, {1} * - 1, PE.Inicio),
                        112
                      )
                      AND CONVERT(
                        VARCHAR,
                        DateADD(DAY, {2}, PE.Fin),
                        112
                      )
                    )
                  )
                  AND SE.IdSeccion = {0}
                  AND AC.EsMatricula = 1
                  AND isnull(FC.CodigoAnterior, '') <> '';
            ";

            await _context.Database.ExecuteSqlRawAsync(insertAlumnosSql, request.IdSeccion, fechaIniDias, fechaFinDias);

            return true;
        }
    }
}

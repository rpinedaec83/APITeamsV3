using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncMissingStudentsCommandHandler : IRequestHandler<SyncMissingStudentsCommand, List<MissingStudentDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncMissingStudentsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<MissingStudentDto>> Handle(SyncMissingStudentsCommand request, CancellationToken cancellationToken)
        {
            // Re-generates TeamsProgramacionGeneral and TeamsProgramacionAlumnos for the section (Option 1 logic)
            // then returns the missing students (Option 4 logic).
            // Step 1: Refresh the programming tables
            var refreshSql = @"
                DELETE TeamsProgramacionGeneral WHERE IdCurso = {0};
                DELETE TeamsProgramacionAlumnos WHERE IdCurso = {0};";
            await _context.Database.ExecuteSqlRawAsync(refreshSql, request.IdSeccion);

            // Step 2: Re-populate TeamsProgramacionGeneral (Option 1 - General)
            var insertGeneralSql = @"
                DECLARE @FechaIniDias INT = 14, @FechaFinDias INT = 14;
                SELECT @FechaIniDias = CONVERT(INT, Valor), @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK) WHERE Nombre = 'EsTeams';

                INSERT INTO TeamsProgramacionGeneral
                SELECT DISTINCT SD.IdSede, SD.Nombre, FA.IdFacultad, FA.Nombre,
                  UN.IdUnidadNegocio, UN.Nombre, UA.IdUnidadAcademica, UA.Nombre,
                  PE.IdPeriodo, PE.Codigo, PD.IdProducto, PD.ProductoNombre,
                  PR.IdPromocion, ISNULL(MTR.Nombre, 'MODULO 0'),
                  PG.IdGrupo, PG.GrupoCodigo, SE.IdSeccion,
                  PD.ProductoCodigo + '.' + ISNULL(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)),
                  PG.GrupoCodigo + ' ' + CU.CursoNombre, CU.CursoNombre,
                  ISNULL(FC.CodigoAnterior, ''),
                  ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), ''),
                  ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), ''),
                  ISNULL(FC.EmailInstitucion, ''), 1, 1, GETDATE()
                FROM Seccion SE WITH (NOLOCK)
                  INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                  INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa
                  INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                  INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad
                  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                  INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                  INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                  INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
                  LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo AND PR.IdCurricula = CM.IdCurricula
                  LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro
                  LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion AND SE.IdGrupo = PG.IdGrupo
                  LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion AND SP.EsResponsable = 1
                  LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
                  LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
                WHERE PE.EsTeams = 1
                  AND ((
                    (PR.TipoServicio = 'P' OR PR.TipoServicio = 'L')
                    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, SE.FechaInicio), 112) AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, SE.FechaFin), 112)
                  ) OR (
                    PR.TipoServicio = 'C'
                    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, PE.Inicio), 112) AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, PE.Fin), 112)
                  ))
                  AND SE.IdSeccion = {0};";
            await _context.Database.ExecuteSqlRawAsync(insertGeneralSql, request.IdSeccion);

            // Step 3: Re-populate TeamsProgramacionAlumnos (Option 1 - Alumnos)
            var insertAlumnosSql = @"
                DECLARE @FechaIniDias INT = 14, @FechaFinDias INT = 14;
                SELECT @FechaIniDias = CONVERT(INT, Valor), @FechaFinDias = CONVERT(INT, Valor)
                FROM Parametro WITH(NOLOCK) WHERE Nombre = 'EsTeams';

                INSERT INTO TeamsProgramacionAlumnos
                SELECT DISTINCT SD.IdSede, SD.Nombre, FA.IdFacultad, FA.Nombre,
                  UN.IdUnidadNegocio, UN.Nombre, UA.IdUnidadAcademica, UA.Nombre,
                  PE.IdPeriodo, PE.Codigo, PD.IdProducto, PD.ProductoNombre,
                  PR.IdPromocion, ISNULL(MTR.Nombre, 'MODULO 0'),
                  PG.IdGrupo, PG.GrupoCodigo, SE.IdSeccion,
                  PD.ProductoCodigo + '.' + ISNULL(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)),
                  PG.GrupoCodigo + ' ' + CU.CursoNombre, CU.CursoNombre,
                  AL.CodigoAnterior,
                  REPLACE(REPLACE(AT.Nombres, 'Ñ', 'N'), '''', ''),
                  REPLACE(REPLACE(AT.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(REPLACE(REPLACE(AT.Materno, 'Ñ', 'N'), '''', ''), ''),
                  AL.EmailInstitucion,
                  ISNULL(FC.CodigoAnterior, ''),
                  ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), ''),
                  ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), ''),
                  ISNULL(FC.EmailInstitucion, ''),
                  AC.Estado, 1, 1, GETDATE()
                FROM Seccion SE WITH (NOLOCK)
                  INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                  INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa
                  INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
                  INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad
                  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
                  INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
                  INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                  INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
                  LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo AND PR.IdCurricula = CM.IdCurricula
                  LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro
                  LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion AND SE.IdGrupo = PG.IdGrupo
                  LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
                  LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion
                  LEFT JOIN Matricula M WITH (NOLOCK) ON M.IdMatricula = AC.IdMatricula AND M.EsMatricula = 1
                  LEFT JOIN Alumno AL WITH (NOLOCK) ON AC.IdAlumno = AL.IdAlumno
                  LEFT JOIN Actor AT WITH (NOLOCK) ON AC.IdAlumno = AT.IdActor
                  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion AND SP.EsResponsable = 1
                  LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
                  LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
                WHERE PE.EsTeams = 1
                  AND ((
                    (PR.TipoServicio = 'P' OR PR.TipoServicio = 'L')
                    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, SE.FechaInicio), 112) AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, SE.FechaFin), 112)
                  ) OR (
                    PR.TipoServicio = 'C'
                    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, PE.Inicio), 112) AND CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, PE.Fin), 112)
                  ))
                  AND SE.IdSeccion = {0}
                  AND AC.EsMatricula = 1
                  AND ISNULL(FC.CodigoAnterior, '') <> '';";
            await _context.Database.ExecuteSqlRawAsync(insertAlumnosSql, request.IdSeccion);

            // Step 4: Return missing students (Option 4 logic)
            var querySql = @"
                SELECT TE.IdTeamsGroup,
                  MPG.CodigoAlumno,
                  MPG.NombresAlumno,
                  MPG.ApellidosAlumno,
                  MPG.EmailAlumno
                FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)
                  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)
                WHERE TE.IdSeccionSmart = {0}
                  AND NOT EXISTS (
                    SELECT 1
                    FROM TeamsUsuarios TU WITH (NOLOCK)
                    WHERE TU.CodigoAlumno = MPG.CodigoAlumno
                      AND TU.idTeams = TE.IdTeamsGroup
                      AND TU.Tipo = 'A'
                      AND TU.Estado = 'A'
                  )
                  AND ISNULL(MPG.CodigoFacilitador, '') <> ''
                  AND TE.EstadoTeam = 'A'";

            return await _context.Database.SqlQueryRaw<MissingStudentDto>(querySql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

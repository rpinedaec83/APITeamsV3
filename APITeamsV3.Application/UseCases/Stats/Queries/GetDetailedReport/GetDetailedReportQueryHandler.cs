using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Data;

namespace APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport
{
    public class GetDetailedReportQueryHandler : IRequestHandler<GetDetailedReportQuery, DetailedReportResultDto>
    {
        private readonly ISmartDbContext _context;

        public GetDetailedReportQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<DetailedReportResultDto> Handle(GetDetailedReportQuery request, CancellationToken cancellationToken)
        {
            var result = new DetailedReportResultDto();

            using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var baseSql = @"
            FROM dbo.AlumnoCurso AC WITH (NOLOCK)
            INNER JOIN dbo.Matricula M WITH (NOLOCK)
                ON M.IdMatricula = AC.IdMatricula AND M.EsMatricula = 1
            INNER JOIN dbo.Alumno AL WITH (NOLOCK) ON AL.IdAlumno = AC.IdAlumno
            INNER JOIN dbo.Actor AA WITH (NOLOCK) ON AA.IdActor = AC.IdAlumno
            INNER JOIN dbo.Seccion SE WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion
            INNER JOIN dbo.Promocion PR WITH (NOLOCK) ON PR.IdPromocion = SE.IdPromocion
            INNER JOIN dbo.Periodo PE WITH (NOLOCK) ON PE.IdPeriodo = PR.IdPeriodo
            INNER JOIN dbo.UnidadNegocio UN WITH (NOLOCK) ON UN.IdUnidadNegocio = PR.IdUnidadNegocio
            INNER JOIN dbo.UnidadAcademica UA WITH (NOLOCK) ON UA.IdUnidadAcademica = PR.IdUnidadAcademica
            INNER JOIN dbo.Producto PD WITH (NOLOCK) ON PD.IdProducto = PR.IdProducto
            INNER JOIN dbo.Curso CU WITH (NOLOCK) ON CU.IdCurso = SE.IdCurso
            LEFT JOIN dbo.PromocionGrupo PG WITH (NOLOCK)
                ON PG.IdPromocion = SE.IdPromocion AND PG.IdGrupo = SE.IdGrupo
            LEFT JOIN dbo.Turno TU WITH (NOLOCK)
                ON TU.IdTurno = COALESCE(SE.IdTurno, PG.IdTurno)
            OUTER APPLY (
                SELECT TOP (1) SP.IdActor, SP.EsResponsable
                FROM dbo.SeccionProfesor SP WITH (NOLOCK)
                WHERE SP.IdSeccion = SE.IdSeccion AND SP.Activo = 1
                ORDER BY CASE SP.EsResponsable WHEN 1 THEN 0 WHEN 4 THEN 1 ELSE 2 END,
                         SP.FechaModificacion DESC,
                         SP.IdActor
            ) SPR
            LEFT JOIN dbo.Facilitador FC WITH (NOLOCK) ON FC.IdFacilitador = SPR.IdActor
            LEFT JOIN dbo.Actor AD WITH (NOLOCK) ON AD.IdActor = SPR.IdActor
            LEFT JOIN dbo.MaestroTablaRegistro MCAT WITH (NOLOCK)
                ON MCAT.IdMaestroRegistro = FC.EsColaborador
            OUTER APPLY (
                SELECT TOP (1) ATD.Descripcion
                FROM dbo.ActorTelefono ATD WITH (NOLOCK)
                WHERE ATD.IdActor = SPR.IdActor AND NULLIF(LTRIM(RTRIM(ATD.Descripcion)), '') IS NOT NULL
                ORDER BY ATD.Principal DESC, ATD.EsVerificado DESC, ATD.FechaModificacion DESC
            ) TD
            OUTER APPLY (
                SELECT TOP (1) FS.IdSede
                FROM dbo.FacilitadorSede FS WITH (NOLOCK)
                WHERE FS.IdFacilitador = SPR.IdActor
                ORDER BY FS.SedePrincipal DESC, FS.FechaModificacion DESC
            ) FSP
            LEFT JOIN dbo.Sede SDP WITH (NOLOCK) ON SDP.IdSede = FSP.IdSede
            LEFT JOIN dbo.MaestroTabla MTRM WITH (NOLOCK) ON MTRM.Codigo = 'TipoFaciltador'
            LEFT JOIN dbo.MaestroTablaRegistro MRESP WITH (NOLOCK)
                ON MRESP.IdMaestroTabla = MTRM.IdMaestroTabla
               AND MRESP.Disponible1 = CONVERT(varchar(10), SPR.EsResponsable)
            OUTER APPLY (
                SELECT TOP (1) ATA.Descripcion
                FROM dbo.ActorTelefono ATA WITH (NOLOCK)
                WHERE ATA.IdActor = AC.IdAlumno AND NULLIF(LTRIM(RTRIM(ATA.Descripcion)), '') IS NOT NULL
                ORDER BY ATA.Principal DESC, ATA.EsVerificado DESC, ATA.FechaModificacion DESC
            ) TA
            OUTER APPLY (
                SELECT TOP (1) EQ.IdTeamsGroup, EQ.FechaCreacion
                FROM dbo.TeamsEquipos EQ WITH (NOLOCK)
                WHERE EQ.IdSeccionSmart = SE.IdSeccion
                ORDER BY EQ.FechaCreacion DESC
            ) TE
            OUTER APPLY (
                SELECT TOP (1) TU2.idUsuario
                FROM dbo.TeamsUsuarios TU2 WITH (NOLOCK)
                WHERE TU2.idTeams = TE.IdTeamsGroup
                  AND TU2.CodigoAlumno = AL.CodigoAnterior
                  AND TU2.Tipo = 'A'
            ) TUS
            OUTER APPLY (
                SELECT TOP (1) SV.UrlClaseVirtual
                FROM dbo.SeccionHorario SV WITH (NOLOCK)
                WHERE SV.IdSeccion = SE.IdSeccion
                  AND NULLIF(LTRIM(RTRIM(SV.UrlClaseVirtual)), '') IS NOT NULL
                ORDER BY SV.IdDia, SV.HoraInicio
            ) SH
            OUTER APPLY (
                SELECT TOP (1) HT.JoinUrl
                FROM dbo.TeamsHorarios HT WITH (NOLOCK)
                WHERE HT.IdTeams = TE.IdTeamsGroup
                  AND HT.CodigoAlumno = AL.CodigoAnterior
                  AND HT.Estado = 'A'
                  AND NULLIF(LTRIM(RTRIM(HT.JoinUrl)), '') IS NOT NULL
                ORDER BY HT.Fecha DESC, HT.NumeroReunion DESC
            ) TH
            WHERE AC.EsMatricula = 1
              AND PE.EsTeams = 1
              AND PE.Inicio >= DATEFROMPARTS(YEAR(GETDATE()) - 1, 1, 1) ";

            var filtersSql = "";

            using var command = connection.CreateCommand();
            
            if (!string.IsNullOrEmpty(request.SmartSearch))
            {
                filtersSql += " AND (UN.Nombre LIKE @SmartSearch OR UA.Nombre LIKE @SmartSearch OR PE.Codigo LIKE @SmartSearch OR SDP.Nombre LIKE @SmartSearch)";
                var param = command.CreateParameter();
                param.ParameterName = "@SmartSearch";
                param.Value = $"%{request.SmartSearch}%";
                command.Parameters.Add(param);
            }

            if (!string.IsNullOrEmpty(request.Unidad))
            {
                filtersSql += " AND UN.Nombre LIKE @Unidad";
                var param = command.CreateParameter();
                param.ParameterName = "@Unidad";
                param.Value = $"%{request.Unidad}%";
                command.Parameters.Add(param);
            }

            if (!string.IsNullOrEmpty(request.Programa))
            {
                filtersSql += " AND UA.Nombre LIKE @Programa";
                var param = command.CreateParameter();
                param.ParameterName = "@Programa";
                param.Value = $"%{request.Programa}%";
                command.Parameters.Add(param);
            }

            if (!string.IsNullOrEmpty(request.Periodo))
            {
                filtersSql += " AND PE.Codigo LIKE @Periodo";
                var param = command.CreateParameter();
                param.ParameterName = "@Periodo";
                param.Value = $"%{request.Periodo}%";
                command.Parameters.Add(param);
            }

            if (!string.IsNullOrEmpty(request.Sede))
            {
                filtersSql += " AND SDP.Nombre LIKE @Sede";
                var param = command.CreateParameter();
                param.ParameterName = "@Sede";
                param.Value = $"%{request.Sede}%";
                command.Parameters.Add(param);
            }

            if (!string.IsNullOrEmpty(request.Docente) || !string.IsNullOrEmpty(request.Alumno))
            {
                filtersSql += " AND (1=0 ";
                if (!string.IsNullOrEmpty(request.Docente))
                {
                    filtersSql += " OR AD.NombreCompleto LIKE @Docente OR FC.CodigoAnterior LIKE @Docente ";
                    var param = command.CreateParameter();
                    param.ParameterName = "@Docente";
                    param.Value = $"%{request.Docente}%";
                    command.Parameters.Add(param);
                }
                if (!string.IsNullOrEmpty(request.Alumno))
                {
                    filtersSql += " OR AA.NombreCompleto LIKE @Alumno OR AL.CodigoAnterior LIKE @Alumno ";
                    var param = command.CreateParameter();
                    param.ParameterName = "@Alumno";
                    param.Value = $"%{request.Alumno}%";
                    command.Parameters.Add(param);
                }
                filtersSql += ") ";
            }

            // Get total count
            command.CommandText = "SELECT COUNT(1) " + baseSql + filtersSql;
            result.TotalRecords = (int)(await command.ExecuteScalarAsync(cancellationToken));

            // Get data
            var paginationSql = $@" 
                ORDER BY UN.Nombre, UA.Nombre, PD.ProductoNombre, SE.Codigo, AL.CodigoAnterior
                OFFSET {(request.PageNumber - 1) * request.PageSize} ROWS 
                FETCH NEXT {request.PageSize} ROWS ONLY ";

            command.CommandText = @"
                SELECT 
                    Division = ISNULL(UN.Nombre, ''),
                    Programa = ISNULL(UA.Nombre, ''),
                    Carrera = ISNULL(PD.ProductoNombre, ''),
                    PeriodoCodigo = ISNULL(PE.Codigo, ''),
                    PeriodoInicio = PE.Inicio,
                    PeriodoFin = PE.Fin,
                    Seccion = ISNULL(SE.Codigo, ''),
                    Turno = ISNULL(TU.Nombre, ''),
                    Curso = ISNULL(CU.CursoNombre, ''),
                    TotalSesiones = ISNULL(SE.CursoSesion, 0),
                    DocenteCodigo = ISNULL(FC.CodigoAnterior, ''),
                    DocenteNombres = ISNULL(AD.NombreCompleto, ''),
                    DocenteCorreo = ISNULL(FC.EmailInstitucion, ''),
                    DocenteCelular = ISNULL(TD.Descripcion, ''),
                    DocenteSedePrincipal = ISNULL(SDP.Nombre, ''),
                    DocenteCategoria = ISNULL(MCAT.Nombre, ''),
                    DocenteTipoResponsable = ISNULL(MRESP.Nombre, ''),
                    M.IdMatricula,
                    AlumnoCelular = ISNULL(TA.Descripcion, ''),
                    Alumno = CONCAT(ISNULL(AL.CodigoAnterior, ''), ' - ', ISNULL(AA.NombreCompleto, '')),
                    AlumnoTipoCondicion = ISNULL(AC.TipoCondicion, ''),
                    Link = COALESCE(NULLIF(SH.UrlClaseVirtual, ''), NULLIF(TH.JoinUrl, ''), ''),
                    DescripcionHorario = ISNULL(PG.DiaClaseTexto, ''),
                    MigroTeams = CONVERT(bit, CASE WHEN TUS.idUsuario IS NULL THEN 0 ELSE 1 END),
                    FechaCreacionEquipoTeams = TE.FechaCreacion " + baseSql + filtersSql + paginationSql + " OPTION (RECOMPILE);";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Data.Add(new DetailedReportDto
                {
                    Division = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Programa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Carrera = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    PeriodoCodigo = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PeriodoInicio = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                    PeriodoFin = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                    Seccion = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Turno = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Curso = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    TotalSesiones = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    DocenteCodigo = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    DocenteNombres = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    DocenteCorreo = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    DocenteCelular = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    DocenteSedePrincipal = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    DocenteCategoria = reader.IsDBNull(15) ? "" : reader.GetString(15),
                    DocenteTipoResponsable = reader.IsDBNull(16) ? "" : reader.GetString(16),
                    IdMatricula = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                    AlumnoCelular = reader.IsDBNull(18) ? "" : reader.GetString(18),
                    Alumno = reader.IsDBNull(19) ? "" : reader.GetString(19),
                    AlumnoTipoCondicion = reader.IsDBNull(20) ? "" : reader.GetString(20),
                    Link = reader.IsDBNull(21) ? "" : reader.GetString(21),
                    DescripcionHorario = reader.IsDBNull(22) ? "" : reader.GetString(22),
                    MigroTeams = !reader.IsDBNull(23) && reader.GetBoolean(23),
                    FechaCreacionEquipoTeams = reader.IsDBNull(24) ? (DateTime?)null : reader.GetDateTime(24),
                });
            }

            return result;
        }
    }
}

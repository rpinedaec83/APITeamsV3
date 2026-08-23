using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetPendingSectionsByPeriodQueryHandler : IRequestHandler<GetPendingSectionsByPeriodQuery, List<PendingSectionDto>>
    {
        private readonly ISmartDbContext _context;
        private readonly ILogger<GetPendingSectionsByPeriodQueryHandler> _logger;

        public GetPendingSectionsByPeriodQueryHandler(
            ISmartDbContext context,
            ILogger<GetPendingSectionsByPeriodQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PendingSectionDto>> Handle(GetPendingSectionsByPeriodQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CodigoPeriodo)) return new List<PendingSectionDto>();

            // Extraer YYYYMMDD directamente del string para evitar desplazamientos por zona horaria UTC
            string? targetDateYmd = null;
            if (!string.IsNullOrWhiteSpace(request.FechaInicioClases))
            {
                var match = Regex.Match(request.FechaInicioClases, @"(\d{4})-(\d{2})-(\d{2})");
                if (match.Success)
                {
                    targetDateYmd = $"{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value}";
                }
                else if (DateTime.TryParse(request.FechaInicioClases, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    targetDateYmd = dt.ToString("yyyyMMdd");
                }
            }

            // Primer intento con filtro de fecha si se especificó
            var result = await QueryPendingSectionsAsync(request.CodigoPeriodo.Trim(), targetDateYmd, cancellationToken);

            // Reintento de respaldo: si el filtro de fecha devuelve 0 registros pero hay fecha especificada, consultar todo el periodo
            if (result.Count == 0 && targetDateYmd != null)
            {
                result = await QueryPendingSectionsAsync(request.CodigoPeriodo.Trim(), null, cancellationToken);
            }

            return result;
        }

        private async Task<List<PendingSectionDto>> QueryPendingSectionsAsync(string codigoPeriodo, string? targetDateYmd, CancellationToken cancellationToken)
        {
            var result = new List<PendingSectionDto>();

            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                }

                using var command = conn.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT
                        SE.IdSeccion,
                        ISNULL(CU.CursoNombre, 'Sin Nombre') AS NombreCurso,
                        ISNULL(PG.GrupoCodigo, ISNULL(SE.Codigo, CAST(SE.IdSeccion AS VARCHAR))) AS CodigoSeccion,
                        ISNULL(SD.Nombre, 'Sin Sede') AS Sede,
                        ISNULL(PD.ProductoNombre, 'Sin Programa') AS Programa,
                        ISNULL(TPG.EmailFacilitador, ISNULL(FC.EmailInstitucion, '')) AS EmailFacilitador,
                        ISNULL(
                            NULLIF(TRIM(ISNULL(TPG.NombresFacilitador, '') + ' ' + ISNULL(TPG.ApellidosFacilitador, '')), ''),
                            ISNULL(NULLIF(TRIM(ISNULL(AT2.Nombres, '') + ' ' + ISNULL(AT2.Paterno, '')), ''), 'Sin Docente')
                        ) AS NombreFacilitador,
                        CASE WHEN TPG.IdCurso IS NOT NULL THEN 1 ELSE 0 END AS HasMetadata
                    FROM Seccion SE WITH(NOLOCK)
                    INNER JOIN Promocion PR WITH(NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    INNER JOIN Periodo PE WITH(NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                    LEFT JOIN Sede SD WITH(NOLOCK) ON PR.IdSede = SD.IdSede
                    LEFT JOIN Producto PD WITH(NOLOCK) ON PR.IdProducto = PD.IdProducto
                    LEFT JOIN Curso CU WITH(NOLOCK) ON SE.IdCurso = CU.IdCurso
                    LEFT JOIN PromocionGrupo PG WITH(NOLOCK) ON SE.IdPromocion = PG.IdPromocion AND SE.IdGrupo = PG.IdGrupo
                    LEFT JOIN SeccionProfesor SP WITH(NOLOCK) ON SP.IdSeccion = SE.IdSeccion AND SP.EsResponsable = 1
                    LEFT JOIN Actor AT2 WITH(NOLOCK) ON SP.IdActor = AT2.IdActor
                    LEFT JOIN Facilitador FC WITH(NOLOCK) ON SP.IdActor = FC.IdFacilitador
                    LEFT JOIN TeamsProgramacionGeneral TPG WITH(NOLOCK) ON SE.IdSeccion = TPG.IdCurso
                    LEFT JOIN TeamsEquipos TE WITH(NOLOCK) ON SE.IdSeccion = TE.IdSeccionSmart AND TE.EstadoTeam = 'A'
                    WHERE PE.Codigo = @CodigoPeriodo
                      AND PE.EsTeams = 1
                      AND (TE.IdTeamsGroup IS NULL OR TE.EstadoTeam <> 'A')
                      AND (@TargetDateYmd IS NULL OR CONVERT(VARCHAR, CASE WHEN PR.TipoServicio = 'C' THEN PE.Inicio ELSE SE.FechaInicio END, 112) = @TargetDateYmd)
                    ORDER BY SE.IdSeccion ASC";

                var paramCodigo = command.CreateParameter();
                paramCodigo.ParameterName = "@CodigoPeriodo";
                paramCodigo.Value = codigoPeriodo;
                command.Parameters.Add(paramCodigo);

                var paramFecha = command.CreateParameter();
                paramFecha.ParameterName = "@TargetDateYmd";
                paramFecha.Value = (object?)targetDateYmd ?? DBNull.Value;
                command.Parameters.Add(paramFecha);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(new PendingSectionDto
                    {
                        IdSeccion = reader.GetInt32(0),
                        NombreCurso = reader.GetString(1),
                        CodigoSeccion = reader.GetString(2),
                        Sede = reader.GetString(3),
                        Programa = reader.GetString(4),
                        EmailFacilitador = reader.GetString(5),
                        NombreFacilitador = reader.GetString(6),
                        HasMetadata = reader.GetInt32(7) == 1
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar secciones pendientes para el periodo {CodigoPeriodo}", codigoPeriodo);
            }

            return result;
        }
    }
}

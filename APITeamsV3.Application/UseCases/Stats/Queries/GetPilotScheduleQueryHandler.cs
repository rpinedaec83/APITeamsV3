using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Stats.Queries
{
    public class GetPilotScheduleQueryHandler : IRequestHandler<GetPilotScheduleQuery, List<PilotScheduleItemDto>>
    {
        private readonly ISmartDbContext _context;

        public GetPilotScheduleQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<PilotScheduleItemDto>> Handle(GetPilotScheduleQuery request, CancellationToken cancellationToken)
        {
            var rawList = new List<PilotScheduleItemDto>();

            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                }

                using var command = conn.CreateCommand();
                command.CommandText = @"
                    DECLARE @FechaIniDias INT = 14,
                            @FechaFinDias INT = 14,
                            @HasPilotTable BIT = 0;

                    SELECT @FechaIniDias = ISNULL(CONVERT(INT, Valor), 14),
                           @FechaFinDias = ISNULL(CONVERT(INT, Valor), 14)
                    FROM Parametro WITH(NOLOCK)
                    WHERE Nombre = 'EsTeams';

                    IF OBJECT_ID(N'[dbo].[TeamsSeccionesPiloto]', N'U') IS NOT NULL
                        SET @HasPilotTable = 1;

                    SELECT 
                        PE.Codigo AS CodigoPeriodo,
                        DATEADD(DAY, @FechaIniDias * -1, CASE WHEN PR.TipoServicio = 'C' THEN PE.Inicio ELSE SE.FechaInicio END) AS FechaInicioCreacion,
                        CASE WHEN PR.TipoServicio = 'C' THEN PE.Inicio ELSE SE.FechaInicio END AS FechaInicioClases,
                        DATEADD(DAY, @FechaFinDias, CASE WHEN PR.TipoServicio = 'C' THEN PE.Fin ELSE SE.FechaFin END) AS FechaFinSincronizacion,
                        COUNT(SE.IdSeccion) AS TotalSecciones,
                        SUM(CASE WHEN TE.EstadoTeam = 'A' THEN 1 ELSE 0 END) AS Creados,
                        SUM(CASE WHEN TE.IdTeamsGroup IS NULL OR TE.EstadoTeam <> 'A' THEN 1 ELSE 0 END) AS Pendientes,
                        CASE 
                            WHEN CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * -1, CASE WHEN PR.TipoServicio = 'C' THEN PE.Inicio ELSE SE.FechaInicio END), 112)
                             AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, CASE WHEN PR.TipoServicio = 'C' THEN PE.Fin ELSE SE.FechaFin END), 112)
                            THEN 1 ELSE 0 
                        END AS EnVentanaHoy,
                        CASE 
                            WHEN @HasPilotTable = 1 THEN SUM(CASE WHEN PIL.IdSeccion IS NOT NULL THEN 1 ELSE 0 END)
                            ELSE COUNT(SE.IdSeccion)
                        END AS SeccionesEnPiloto
                    FROM Seccion SE WITH(NOLOCK)
                    INNER JOIN Promocion PR WITH(NOLOCK) ON SE.IdPromocion = PR.IdPromocion
                    INNER JOIN Periodo PE WITH(NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
                    LEFT JOIN TeamsEquipos TE WITH(NOLOCK) ON SE.IdSeccion = TE.IdSeccionSmart AND TE.EstadoTeam = 'A'
                    LEFT JOIN dbo.TeamsSeccionesPiloto PIL WITH(NOLOCK) ON SE.IdSeccion = PIL.IdSeccion AND PIL.EsActivo = 1
                    WHERE PE.EsTeams = 1 
                      AND CONVERT(VARCHAR, GETDATE(), 112) <= CONVERT(VARCHAR, DATEADD(DAY, @FechaFinDias, CASE WHEN PR.TipoServicio = 'C' THEN PE.Fin ELSE SE.FechaFin END), 112)
                    GROUP BY 
                        PE.Codigo,
                        CASE WHEN PR.TipoServicio = 'C' THEN PE.Inicio ELSE SE.FechaInicio END,
                        CASE WHEN PR.TipoServicio = 'C' THEN PE.Fin ELSE SE.FechaFin END
                    ORDER BY FechaInicioCreacion ASC";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var today = DateTime.Today;

                while (await reader.ReadAsync(cancellationToken))
                {
                    var codigoPeriodo = reader.GetString(0);
                    var inicioCreacion = reader.GetDateTime(1);
                    var inicioClases = reader.GetDateTime(2);
                    var finSinc = reader.GetDateTime(3);
                    var total = Convert.ToInt32(reader.GetValue(4));
                    var creados = Convert.ToInt32(reader.GetValue(5));
                    var pendientes = Convert.ToInt32(reader.GetValue(6));
                    var enVentana = Convert.ToInt32(reader.GetValue(7)) == 1;
                    var seccionesEnPiloto = Convert.ToInt32(reader.GetValue(8));

                    string estadoStr;
                    if (creados >= total && total > 0)
                    {
                        estadoStr = "COMPLETADO";
                    }
                    else if (enVentana)
                    {
                        estadoStr = "EN_VENTANA_ACTIVA";
                    }
                    else if (today < inicioCreacion)
                    {
                        estadoStr = "PROXIMA_GESTION";
                    }
                    else
                    {
                        estadoStr = "FINALIZADO";
                    }

                    rawList.Add(new PilotScheduleItemDto
                    {
                        CodigoPeriodo = codigoPeriodo,
                        FechaInicioCreacion = inicioCreacion,
                        FechaInicioClases = inicioClases,
                        FechaFinSincronizacion = finSinc,
                        TotalSecciones = total,
                        Creados = creados,
                        Pendientes = pendientes,
                        EnVentanaHoy = enVentana,
                        EstadoGestion = estadoStr,
                        EsPiloto = seccionesEnPiloto > 0
                    });
                }
            }
            catch (Exception)
            {
                // Return empty list safely on unexpected errors
            }

            return rawList;
        }
    }
}

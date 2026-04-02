using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Persistence;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class DatabaseInitializerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializerService> _logger;

        public DatabaseInitializerService(IServiceProvider serviceProvider, ILogger<DatabaseInitializerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DatabaseInitializerService started.");

            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<CentralDbContext>();
                var encryptionService = services.GetRequiredService<IEncryptionService>();

                // Update View for SmartDbContext for ALL active tenants
                var configs = await context.CompanyConfigs.Where(c => c.IsActive).ToListAsync(stoppingToken);
                _logger.LogInformation("Updating views for {Count} active tenants...", configs.Count);

                foreach (var config in configs)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        var connectionString = encryptionService.Decrypt(config.SmartConnectionString);

                        var optionsBuilder = new DbContextOptionsBuilder<SmartDbContext>();
                        optionsBuilder.UseSqlServer(connectionString, sqlOptions => 
                        {
                            sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 3,
                                maxRetryDelay: TimeSpan.FromSeconds(10),
                                errorNumbersToAdd: null);
                        });

                        using (var directSmartContext = new SmartDbContext(optionsBuilder.Options, null!))
                        {
                            var viewSql = GetViewSql();
                            // Use a timeout for the SQL command to avoid hanging the background service indefinitely
                            directSmartContext.Database.SetCommandTimeout(30);
                            await directSmartContext.Database.ExecuteSqlRawAsync(viewSql, stoppingToken);
                        }
                        _logger.LogInformation("Successfully updated view for tenant {CompanyKey}.", config.CompanyKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not update view for tenant {CompanyKey}: {Message}", config.CompanyKey, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during background database initialization.");
            }

            _logger.LogInformation("DatabaseInitializerService finished initial setup.");
        }

        private string GetViewSql()
        {
            return @"
CREATE OR ALTER VIEW [dbo].[vw_MatriculasActivas] AS
SELECT SE.IdSeccion,
    SE.Codigo,
    SE.IdCurso,
    SE.IdPromocion,
    ISNULL(PR.IdCurricula, 0) AS IdCurricula,
    PE.IdPeriodo,
    SE.FechaInicio,
    SE.FechaFin,
    -- Extended Info
    CU.CursoNombre,
    PD.ProductoCodigo,
    PD.ProductoNombre,
    SD.Nombre AS SedeNombre,
    UN.Nombre AS UnidadNegocioNombre,
    UA.Nombre AS UnidadAcademicaNombre,
    PE.Codigo AS CodigoPeriodo,
    PG.GrupoCodigo,
    -- Date Filter Logic
    PR.TipoServicio,
    PE.Inicio AS PeriodoInicio,
    PE.Fin AS PeriodoFin,
    -- Facilitador
    ISNULL(FC.CodigoAnterior, '') AS CodigoFacilitador,
    ISNULL(
        REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', '') + ' ' + REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(
            REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''),
            ''
        ),
        ''
    ) AS NombresFacilitador,
    ISNULL(FC.EmailInstitucion, '') AS EmailFacilitador,
    -- Flags
    CAST(ISNULL(PE.EsTeams, 0) AS BIT) AS EsTeams,
    -- Calculated Placeholders (EF requires them if mapped)
    CAST('' AS NVARCHAR(100)) AS CalculatedMailNickname,
    CAST('' AS NVARCHAR(100)) AS CalculatedDisplayName
FROM Seccion SE WITH (NOLOCK)
    LEFT JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
    LEFT JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
    LEFT JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
    LEFT JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
    LEFT JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
    LEFT JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
    LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
    AND SE.IdGrupo = PG.IdGrupo
    LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
    LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
    AND SP.EsResponsable = 1
    LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
    LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador;";
        }
    }
}

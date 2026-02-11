using APITeamsV3.Application;
using APITeamsV3.Infrastructure;
using APITeamsV3.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Application.Common.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "https://teams.localhost:3000") // Add your frontend origins
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<APITeamsV3.API.Filters.TenantHeaderFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Force HTTPS

app.UseCors("AllowAll");

app.UseAuthorization();

// Multi-tenancy Middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

// Ensure DB Created and Seeded (Basic check for dev)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CentralDbContext>();
        var encryptionService = services.GetRequiredService<IEncryptionService>();
        context.Database.Migrate();
        await APITeamsV3.Infrastructure.Persistence.CentralDbContextSeed.SeedAsync(context, encryptionService);
        
        // Update View for SmartDbContext
        // CRITICAL: We must manually fetch the connection string for the default tenant ('idat') 
        // because ITenantProvider is empty outside of an HTTP request.
        var idatConfig = await context.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == "idat");
        if (idatConfig != null)
        {
            var connectionString = encryptionService.Decrypt(idatConfig.SmartConnectionString);
            
            var optionsBuilder = new DbContextOptionsBuilder<SmartDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            
            // We can pass null for TenantProvider as we configured the DB context with specific options
            using (var directSmartContext = new SmartDbContext(optionsBuilder.Options, null)) 
            {
                 // Force View Update with Embedded SQL
                var viewSql = @"
CREATE OR ALTER VIEW [dbo].[vw_MatriculasActivas] AS
SELECT SE.IdSeccion,
    SE.Codigo,
    SE.IdCurso,
    SE.IdPromocion,
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
    PE.Codigo AS CodigoPeriodo, -- Duplicate removed in map
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
    -- Calculated Placeholders (EF requires them if mapped)
    CAST('' AS NVARCHAR(100)) AS CalculatedMailNickname,
    CAST('' AS NVARCHAR(100)) AS CalculatedDisplayName
FROM Seccion SE WITH (NOLOCK)
    INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
    INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
    INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
    INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
    INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
    INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
    LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
    AND SE.IdGrupo = PG.IdGrupo
    LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
    LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
    AND SP.EsResponsable = 1
    LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
    LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
WHERE PE.EsTeams = 1;";

                await directSmartContext.Database.ExecuteSqlRawAsync(viewSql);
            
            }
        }
        else
        {
             var logger = services.GetRequiredService<ILogger<Program>>();
             logger.LogWarning("DEBUG: idatConfig NOT FOUND in CentralDbContext. View vw_MatriculasActivas NOT UPDATED.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

app.Run();

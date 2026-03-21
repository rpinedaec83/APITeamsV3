using APITeamsV3.Application;
using APITeamsV3.Infrastructure;
using APITeamsV3.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Application.Common.Interfaces;
using Hangfire;
using Hangfire.MemoryStorage;

using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
    options => {
        options.TokenValidationParameters.ValidAudiences = new[] { 
            "c356c453-9a02-48ae-90fe-6a55af698a60", 
            "0a769a7f-b15f-49b3-832b-f4755ced41d1", // Zegel SPA
            "0856381c-a0f4-4e74-b1c4-23d6710e5d53",  // Idat SPA
            "0fbcd069-7033-407b-91db-c7943513a078",  // CA SPA (Placeholder if needed)
            "4c7ba983-34bd-44a6-848e-670560b411d3"   // ITS SPA
        };
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire Configuration (Memory Storage for Dev)
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

builder.Services.AddHangfireServer();

// Sync Scheduler Background Service
builder.Services.AddHostedService<APITeamsV3.Infrastructure.Services.SyncSchedulerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "https://teams.zegel.edu.pe",
                    "https://teams.idat.edu.pe",
                    "https://teams.corrientealterna.edu.pe",
                    "https://teams.its.edu.pe",
                    "https://teams.centrodelaimagen.pe"
                )
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

// Hangfire Dashboard
app.UseHangfireDashboard();

// app.UseHttpsRedirection(); // Force HTTPS

app.UseCors("AllowAll");

app.UseAuthentication();
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
        
        // Update View for SmartDbContext for ALL active tenants
        var configs = await context.CompanyConfigs.Where(c => c.IsActive).ToListAsync();
        foreach (var config in configs)
        {
            try 
            {
                var connectionString = encryptionService.Decrypt(config.SmartConnectionString);
                
                var optionsBuilder = new DbContextOptionsBuilder<SmartDbContext>();
                optionsBuilder.UseSqlServer(connectionString);
                
                using (var directSmartContext = new SmartDbContext(optionsBuilder.Options, null!)) 
                {
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

                    await directSmartContext.Database.ExecuteSqlRawAsync(viewSql);
                }
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning($"Could not update view for tenant {config.CompanyKey}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

app.Run();

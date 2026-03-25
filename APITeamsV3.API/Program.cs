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
builder.Services.AddHttpContextAccessor();
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

// 1. Enable CORS for ALL responses (including errors)
app.UseCors("AllowAll");

// 2. Global Exception Handling (ensure JSON response on crash)
app.UseMiddleware<APITeamsV3.API.Middleware.ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "APITeamsV3.API v1");
    c.RoutePrefix = "swagger"; // This ensures it's at /swagger
});

// Hangfire Dashboard
app.UseHangfireDashboard();

app.UseHttpsRedirection(); // Force HTTPS

app.UseAuthentication();
app.UseAuthorization();

// Multi-tenancy Middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

// Ensure Central DB is ready (Essential for tenant resolution)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CentralDbContext>();
        var encryptionService = services.GetRequiredService<IEncryptionService>();
        context.Database.Migrate(); // SQLite migration is fast
        await APITeamsV3.Infrastructure.Persistence.CentralDbContextSeed.SeedAsync(context, encryptionService);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred migrating the central DB.");
    }
}

app.Run();

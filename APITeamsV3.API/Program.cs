using APITeamsV3.Application;
using APITeamsV3.Infrastructure;
using APITeamsV3.Infrastructure.MultiTenancy;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        var apiClientId = builder.Configuration["AzureAd:ClientId"];
        if (string.IsNullOrWhiteSpace(apiClientId))
        {
            throw new InvalidOperationException("AzureAd:ClientId must be configured.");
        }

        options.TokenValidationParameters.ValidAudiences = new[] { apiClientId };
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<HangfireServiceScopeJobActivator>();
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TenantHangfireRuntime>());

builder.Services.AddHostedService<SyncSchedulerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
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

app.UseCors("AllowAll");
app.UseMiddleware<APITeamsV3.API.Middleware.ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "APITeamsV3.API v1");
    c.RoutePrefix = "swagger";
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<CentralDbContext>();
        context.Database.Migrate();
        await APITeamsV3.Infrastructure.Persistence.CentralDbContextSeed.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred migrating the central DB.");
    }
}

GlobalConfiguration.Configuration.UseActivator(app.Services.GetRequiredService<HangfireServiceScopeJobActivator>());

var runtime = app.Services.GetRequiredService<TenantHangfireRuntime>();
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var dashboardRegistrations = await runtime.GetDashboardRegistrationsAsync();

foreach (var registration in dashboardRegistrations)
{
    var dashboardPath = $"/hangfire/{registration.CompanyKey}";
    app.UseHangfireDashboard(dashboardPath, new DashboardOptions(), registration.Storage);
    startupLogger.LogInformation("Mapped Hangfire dashboard route {DashboardPath}.", dashboardPath);
}

if (dashboardRegistrations.Count == 0)
{
    startupLogger.LogWarning("No tenant Hangfire dashboards were mapped because there are no active tenants with SmartConnectionString configured.");
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();

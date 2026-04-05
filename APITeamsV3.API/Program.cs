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
        var configuration = builder.Configuration;
        var masterClientId = configuration["AzureAd:ClientId"];
        
        options.TokenValidationParameters.AudienceValidator = (audiences, securityToken, validationParameters) =>
        {
            if (audiences == null || !audiences.Any()) return false;

            // 1. Check against Master Client ID
            if (audiences.Any(a => string.Equals(a, masterClientId, StringComparison.OrdinalIgnoreCase))) return true;

            // 2. Check against Database Client IDs using direct connection for performance and stability
            try 
            {
                var dbAudiences = new List<string>();
                var connectionString = configuration.GetConnectionString("CentralConnection") ?? "Data Source=APITeamsV3_Central.db";
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT ApiClientId FROM CompanyConfigs WHERE IsActive = 1 AND ApiClientId IS NOT NULL AND ApiClientId != ''";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read()) dbAudiences.Add(reader.GetString(0));
                        }
                    }
                }

                if (audiences.Any(a => dbAudiences.Any(da => string.Equals(a, da, StringComparison.OrdinalIgnoreCase)))) return true;
                if (audiences.Any(a => dbAudiences.Any(da => string.Equals(a, $"api://{da}", StringComparison.OrdinalIgnoreCase)))) return true;
            }
            catch 
            {
                // Fallback to false if DB is locked or failing during validation
                return false;
            }

            return false;
        };
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<HangfireServiceScopeJobActivator>();
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TenantHangfireRuntime>());
builder.Services.AddHostedService<RecordingTransferRecurringJobRegistrar>();

builder.Services.AddHostedService<SyncSchedulerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
                .SetIsOriginAllowed(origin => true) // More flexible for debugging/multiple domains
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

app.UseRouting();
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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.MapGet("/inspect-graph/{groupId}", async (string groupId, APITeamsV3.Application.Common.Interfaces.IGraphClientFactory factory) => 
{
    var client = await factory.CreateClientAsync();
    
    var owners = await client.Groups[groupId].Owners.GetAsync();
    var members = await client.Groups[groupId].Members.GetAsync();
    
    var ownerNames = owners?.Value?.Select(o => o is Microsoft.Graph.Models.User u ? $"{u.DisplayName} ({u.Mail})" : o.Id).ToList();
    var memberNames = members?.Value?.Select(m => m is Microsoft.Graph.Models.User u ? $"{u.DisplayName} ({u.Mail})" : m.Id).ToList();
    
    return Microsoft.AspNetCore.Http.Results.Ok(new { Owners = ownerNames, Members = memberNames });
});

app.Run();

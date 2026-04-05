using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.MultiTenancy;
using APITeamsV3.Infrastructure.Services;
using APITeamsV3.Infrastructure.Options;

namespace APITeamsV3.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Central DB
            services.AddDbContext<CentralDbContext>(options =>
                options.UseSqlite(
                    configuration.GetConnectionString("CentralConnection"),
                    b => b.MigrationsAssembly(typeof(CentralDbContext).Assembly.FullName)));
            services.AddScoped<ICentralDbContext>(provider => provider.GetRequiredService<CentralDbContext>());


            // Smart DB (Multi-tenant)
            services.AddDbContext<SmartDbContext>();
            services.AddScoped<ISmartDbContext>(provider => provider.GetRequiredService<SmartDbContext>());

            // Multi-tenancy
            services.AddScoped<ITenantProvider, TenantProvider>();
            
            // Repositories
            services.AddScoped<ITeamAcademicoRepository, APITeamsV3.Infrastructure.Persistence.Repositories.TeamAcademicoRepository>();
            services.AddScoped<ITeamsLogOperativoRepository, APITeamsV3.Infrastructure.Persistence.Repositories.TeamsLogOperativoRepository>();


            // Services
            services.AddScoped<INamingService, NamingService>();
            services.AddScoped<IGraphClientFactory, GraphClientFactory>();
            services.AddTransient<ITeamProvisioningService, TeamProvisioningService>();
            services.AddScoped<SessionSchedulingService>();
            services.AddScoped<ITeamsAgendaService, TeamsAgendaService>();
            services.AddTransient<IBackgroundJobService, BackgroundJobService>();
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddSingleton<TenantHangfireRuntime>();
            services.AddTransient<IHangfireJobService, HangfireJobService>();
            services.AddScoped<IGraphUserLookupService, GraphUserLookupService>();
            services.AddScoped<ITeamsRecordingTransferService, TeamsRecordingTransferService>();
            services.Configure<RecordingTransferOptions>(configuration.GetSection(RecordingTransferOptions.SectionName));

            // Database Initialization Service (Background)
            services.AddHostedService<DatabaseInitializerService>();

            return services;
        }
    }
}

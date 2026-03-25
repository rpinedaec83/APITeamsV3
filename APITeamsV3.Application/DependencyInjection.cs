using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace APITeamsV3.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
            services.AddScoped<APITeamsV3.Application.Common.Interfaces.ISectionEligibilityService, APITeamsV3.Application.Common.Services.SectionEligibilityService>();
            return services;
        }
    }
}

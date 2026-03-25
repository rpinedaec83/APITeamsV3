using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace APITeamsV3.Infrastructure.Services
{
    public sealed class HangfireServiceScopeJobActivator : JobActivator
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public HangfireServiceScopeJobActivator(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        [Obsolete("Hangfire requires this override for scoped job activation.", false)]
        public override JobActivatorScope BeginScope()
        {
            return new ServiceScopeAdapter(_serviceScopeFactory.CreateScope());
        }

        private sealed class ServiceScopeAdapter : JobActivatorScope
        {
            private readonly IServiceScope _serviceScope;

            public ServiceScopeAdapter(IServiceScope serviceScope)
            {
                _serviceScope = serviceScope;
            }

            public override object Resolve(Type type)
            {
                return ActivatorUtilities.GetServiceOrCreateInstance(_serviceScope.ServiceProvider, type);
            }

            public override void DisposeScope()
            {
                _serviceScope.Dispose();
                base.DisposeScope();
            }
        }
    }
}

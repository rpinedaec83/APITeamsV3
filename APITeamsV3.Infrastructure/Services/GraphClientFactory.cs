using Azure.Identity;
using Microsoft.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class GraphClientFactory : IGraphClientFactory
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly SmartDbContext _smartContext;
        private readonly ILogger<GraphClientFactory> _logger;

        public GraphClientFactory(
            ITenantProvider tenantProvider,
            SmartDbContext smartContext,
            ILogger<GraphClientFactory> logger)
        {
            _tenantProvider = tenantProvider;
            _smartContext = smartContext;
            _logger = logger;
        }

        public Task<GraphServiceClient> CreateClientAsync()
        {
            var tenant = _tenantProvider.GetCurrentTenant();

            // Client Credentials Flow
            // Ensure TenantId, ClientId, ClientSecret are present
            if (string.IsNullOrEmpty(tenant.GraphTenantId) || 
                string.IsNullOrEmpty(tenant.GraphClientId) || 
                string.IsNullOrEmpty(tenant.GraphClientSecret))
            {
                throw new System.Exception($"Graph configuration missing for tenant {tenant.CompanyKey}");
            }

            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var clientSecretCredential = new ClientSecretCredential(
                tenant.GraphTenantId,
                tenant.GraphClientId,
                tenant.GraphClientSecret,
                options);

            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            return Task.FromResult(graphClient);
        }

        public async Task<GraphServiceClient> CreateDelegatedClientAsync()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            var graphTenantId = (tenant.GraphTenantId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(graphTenantId))
            {
                throw new System.InvalidOperationException("No se encontro GraphTenantId para token delegado.");
            }

            var appAccount = await _smartContext.AplicativosTeams
                .AsNoTracking()
                .Where(a => a.Activo == "A" && a.TenantId == graphTenantId)
                .OrderBy(a => a.IdAplicativo)
                .FirstOrDefaultAsync();

            if (appAccount == null)
            {
                throw new System.InvalidOperationException(
                    $"No existe cuenta tecnica activa en AplicativosTeams para tenant {graphTenantId}.");
            }

            var username = (appAccount.UsernameApp ?? string.Empty).Trim();
            var password = (appAccount.PasswordApp ?? string.Empty).Trim();
            var clientId = (appAccount.AppClientId ?? string.Empty).Trim();
            var tenantId = (appAccount.TenantId ?? graphTenantId).Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(tenantId))
            {
                throw new System.InvalidOperationException(
                    "La cuenta tecnica activa no tiene UsernameApp/PasswordApp/AppClientId/TenantId completos para token delegado.");
            }

#pragma warning disable CS0618
            var options = new UsernamePasswordCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var delegatedCredential = new UsernamePasswordCredential(
                username,
                password,
                tenantId,
                clientId,
                options);
#pragma warning restore CS0618

            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var graphClient = new GraphServiceClient(delegatedCredential, scopes);

            _logger.LogInformation(
                "Created delegated Graph client with technical account {Username} for tenant {TenantId}.",
                username,
                tenantId);

            return graphClient;
        }
    }
}

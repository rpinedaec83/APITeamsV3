using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using APITeamsV3.Application.Common.Interfaces;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class GraphClientFactory : IGraphClientFactory
    {
        private readonly ITenantProvider _tenantProvider;

        public GraphClientFactory(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
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
    }
}

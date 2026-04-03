using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Data.Sqlite;
using Azure.Identity;
using Microsoft.Graph;
using Xunit.Abstractions;

namespace GraphTest
{
    public class GraphQueryTest
    {
        private readonly ITestOutputHelper _output;

        public GraphQueryTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task QueryGroupMembers()
        {
            string tenantId = "";
            string clientId = "";
            string clientSecret = "";

            string connectionString = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\APITeamsV3_Central.db";
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT GraphTenantId, GraphClientId, GraphClientSecretRef FROM CompanyConfigs WHERE CompanyKey = 'zegel' LIMIT 1";
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        tenantId = reader.GetString(0);
                        clientId = reader.GetString(1);
                        clientSecret = reader.GetString(2);
                    }
                }
            }

            Assert.NotEmpty(tenantId);

            var options = new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud };
            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            string groupId = "a57f02b1-1f41-4c87-8da1-7f0f2ed4869b";

            _output.WriteLine($"--- OWNERS FOR GROUP {groupId} ---");
            var owners = await graphClient.Groups[groupId].Owners.GetAsync();
            if (owners?.Value != null) {
                foreach (var owner in owners.Value) {
                    if (owner is Microsoft.Graph.Models.User userOwner)
                        _output.WriteLine($"Owner: {userOwner.DisplayName} ({userOwner.Mail} / {userOwner.UserPrincipalName})");
                }
            }

            _output.WriteLine($"\n--- MEMBERS FOR GROUP {groupId} ---");
            var members = await graphClient.Groups[groupId].Members.GetAsync();
            if (members?.Value != null) {
                foreach (var member in members.Value) {
                    if (member is Microsoft.Graph.Models.User userMember)
                        _output.WriteLine($"Member: {userMember.DisplayName} ({userMember.Mail} / {userMember.UserPrincipalName})");
                }
            }
        }
    }
}

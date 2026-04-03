using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Azure.Identity;
using Microsoft.Graph;

class Program
{
    static async Task Main()
    {
        string tenantId = "";
        string clientId = "";
        string clientSecret = "";

        string connectionString = @"Data Source=c:\Sources\APITeamsV3\APITeamsV3.API\APITeamsV3_Central.db";
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT GraphTenantId, GraphClientId, GraphClientSecret FROM CompanyConfigs WHERE CompanyKey = 'zegel' LIMIT 1";
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

        Console.WriteLine($"Auth Info: TenantId={tenantId}, ClientId={clientId}");
        if (string.IsNullOrEmpty(tenantId)) return;

        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

        string groupId = "a57f02b1-1f41-4c87-8da1-7f0f2ed4869b"; // Group ID from user

        try 
        {
            Console.WriteLine("--- OWNERS ---");
            var owners = await graphClient.Groups[groupId].Owners.GetAsync();
            if (owners?.Value != null) {
                foreach (var owner in owners.Value) {
                    if (owner is Microsoft.Graph.Models.User userOwner)
                        Console.WriteLine($"Owner: {userOwner.DisplayName} ({userOwner.Mail})");
                }
            }

            Console.WriteLine("\n--- MEMBERS ---");
            var members = await graphClient.Groups[groupId].Members.GetAsync();
            if (members?.Value != null) {
                foreach (var member in members.Value) {
                    if (member is Microsoft.Graph.Models.User userMember)
                        Console.WriteLine($"Member: {userMember.DisplayName} ({userMember.Mail})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graph API Error: {ex.Message}");
        }
    }
}

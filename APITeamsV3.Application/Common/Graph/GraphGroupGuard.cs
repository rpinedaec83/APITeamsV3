using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Graph
{
    public static class GraphGroupGuard
    {
        public static async Task<bool> GroupExistsAsync(
            GraphServiceClient client,
            string groupId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return false;
            }

            try
            {
                var group = await client.Groups[groupId].GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = new[] { "id" };
                    },
                    cancellationToken);

                return !string.IsNullOrWhiteSpace(group?.Id);
            }
            catch (Exception ex) when (IsMissingResource(ex))
            {
                return false;
            }
        }

        public static bool IsMissingResource(Exception ex)
        {
            if (ex is ODataError odataError && odataError.ResponseStatusCode == 404)
            {
                return true;
            }

            if (ex is ApiException apiException && apiException.ResponseStatusCode == 404)
            {
                return true;
            }

            return ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }
    }
}

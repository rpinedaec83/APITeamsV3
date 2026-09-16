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
            if (ex is ODataError odataError)
            {
                if (odataError.ResponseStatusCode == 404) return true;
                if (odataError.Message != null && odataError.Message.Contains("requested group", StringComparison.OrdinalIgnoreCase) && odataError.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (ex is ApiException apiException && apiException.ResponseStatusCode == 404)
            {
                return true;
            }

            var message = ex.Message ?? string.Empty;
            var fullText = ex.ToString() ?? string.Empty;

            return message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("resource not found", StringComparison.OrdinalIgnoreCase) ||
                   (message.Contains("requested group", StringComparison.OrdinalIgnoreCase) && message.Contains("invalid", StringComparison.OrdinalIgnoreCase)) ||
                   (fullText.Contains("requested group", StringComparison.OrdinalIgnoreCase) && fullText.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        }
    }
}

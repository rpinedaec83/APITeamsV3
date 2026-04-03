using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Graph
{
    public static class EducationClassResolver
    {
        public static async Task<string?> ResolveClassIdFromGroupIdAsync(
            GraphServiceClient client,
            string groupId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return null;
            }

            // In some tenants, educationClass id == group id.
            try
            {
                var direct = await client.Education.Classes[groupId].GetAsync(cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(direct?.Id))
                {
                    return direct.Id;
                }
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                // Continue with mailNickname resolution.
            }

            string? mailNickname = null;
            try
            {
                var group = await client.Groups[groupId].GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = new[] { "id", "mailNickname" };
                    },
                    cancellationToken);

                mailNickname = group?.MailNickname;
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(mailNickname))
            {
                return null;
            }

            var escaped = EscapeODataLiteral(mailNickname);
            var classes = await client.Education.Classes.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"mailNickname eq '{escaped}'";
                    requestConfiguration.QueryParameters.Top = 5;
                },
                cancellationToken);

            return classes?.Value?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Id))?.Id;
        }

        public static async Task<string?> ResolveGroupIdFromClassIdAsync(
            GraphServiceClient client,
            string classId,
            string? fallbackMailNickname = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return null;
            }

            // In some tenants, class id == group id.
            try
            {
                var group = await client.Groups[classId].GetAsync(cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(group?.Id))
                {
                    return group.Id;
                }
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                // Continue with class + mailNickname fallback.
            }

            string? mailNickname = fallbackMailNickname;
            try
            {
                var educationClass = await client.Education.Classes[classId].GetAsync(cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(educationClass?.MailNickname))
                {
                    mailNickname = educationClass.MailNickname;
                }
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                // Continue with fallback nickname.
            }

            if (string.IsNullOrWhiteSpace(mailNickname))
            {
                return null;
            }

            var escaped = EscapeODataLiteral(mailNickname);
            var groups = await client.Groups.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"mailNickname eq '{escaped}'";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "mailNickname" };
                    requestConfiguration.QueryParameters.Top = 5;
                },
                cancellationToken);

            return groups?.Value?.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Id))?.Id;
        }

        private static string EscapeODataLiteral(string value) => (value ?? string.Empty).Replace("'", "''");

        private static bool IsNotFound(Exception ex)
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
                   ex.Message.Contains("not present", StringComparison.OrdinalIgnoreCase);
        }
    }
}

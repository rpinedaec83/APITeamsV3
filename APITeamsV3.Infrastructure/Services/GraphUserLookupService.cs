using APITeamsV3.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class GraphUserLookupService : IGraphUserLookupService
    {
        private readonly ILogger<GraphUserLookupService> _logger;

        public GraphUserLookupService(ILogger<GraphUserLookupService> logger)
        {
            _logger = logger;
        }

        public async Task<User?> FindUserAsync(GraphServiceClient client, string emailOrNickname, string? altDomain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(emailOrNickname)) return null;

            User? foundUser = null;
            string localPart = emailOrNickname.Split('@')[0];

            // 1. Primary Lookup (Direct match by UPN/Mail)
            try
            {
                _logger.LogDebug($"Lookup Stage 1: Attempting direct lookup for '{emailOrNickname}'");
                foundUser = await client.Users[emailOrNickname].GetAsync(cancellationToken: cancellationToken);
                if (foundUser?.Id != null) return foundUser;
            }
            catch (Exception ex) when (IsNotFoundError(ex)) { /* Expected if not found */ }
            catch (Exception ex)
            {
                _logger.LogWarning($"Primary lookup error for {emailOrNickname}: {ex.Message}");
            }

            // 2. Nickname Lookup (mailNickname eq 'localPart')
            try
            {
                _logger.LogDebug($"Lookup Stage 2: Attempting Nickname lookup for '{localPart}'");
                var usersByNick = await client.Users.GetAsync(q =>
                {
                    q.QueryParameters.Filter = $"mailNickname eq '{localPart}'";
                    q.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "mailNickname" };
                }, cancellationToken: cancellationToken);

                foundUser = usersByNick?.Value?.FirstOrDefault();
                if (foundUser?.Id != null)
                {
                    _logger.LogInformation($"User {emailOrNickname} found via mailNickname fallback ({foundUser.UserPrincipalName}).");
                    return foundUser;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Nickname lookup error for {localPart}: {ex.Message}");
            }

            // 3. Alternative Domain Lookup
            if (!string.IsNullOrWhiteSpace(altDomain))
            {
                string altEmail = $"{localPart}@{altDomain.TrimStart('@')}";
                try
                {
                    _logger.LogDebug($"Lookup Stage 3: Attempting Alternative Domain lookup for '{altEmail}'");
                    var usersByAltEmail = await client.Users.GetAsync(q =>
                    {
                        q.QueryParameters.Filter = $"mail eq '{altEmail}'";
                        q.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "mailNickname" };
                    }, cancellationToken: cancellationToken);

                    foundUser = usersByAltEmail?.Value?.FirstOrDefault();
                    
                    // If not found by mail, try direct Get:
                    if (foundUser == null)
                    {
                        try 
                        {
                            var u = await client.Users[altEmail].GetAsync(cancellationToken: cancellationToken);
                            if (u?.Id != null) foundUser = u;
                        }
                        catch (Exception ex) when (IsNotFoundError(ex)) { }
                    }

                    if (foundUser?.Id != null)
                    {
                        _logger.LogInformation($"User {emailOrNickname} found via Alternative Domain fallback ({foundUser.UserPrincipalName}).");
                        return foundUser;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Alternative domain lookup error for {altEmail}: {ex.Message}");
                }
            }

            // 4. Legacy Domain Lookup (@SEESAC.onmicrosoft.com)
            string legacyEmail = $"{localPart}@SEESAC.onmicrosoft.com";
            try
            {
                _logger.LogDebug($"Lookup Stage 4: Attempting Legacy Domain lookup for '{legacyEmail}'");
                var usersByLegacyEmail = await client.Users.GetAsync(q =>
                {
                    q.QueryParameters.Filter = $"mail eq '{legacyEmail}'";
                    q.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "mailNickname" };
                }, cancellationToken: cancellationToken);

                foundUser = usersByLegacyEmail?.Value?.FirstOrDefault();
                
                // If not found by mail, try direct Get:
                if (foundUser == null)
                {
                    try 
                    {
                        var u = await client.Users[legacyEmail].GetAsync(cancellationToken: cancellationToken);
                        if (u?.Id != null) foundUser = u;
                    }
                    catch (Exception ex) when (IsNotFoundError(ex)) { }
                }

                if (foundUser?.Id != null)
                {
                    _logger.LogInformation($"User {emailOrNickname} found via Legacy Domain fallback ({foundUser.UserPrincipalName}).");
                    return foundUser;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Legacy domain lookup error for {legacyEmail}: {ex.Message}");
            }

            // Not found
            _logger.LogWarning($"User lookup exhausted all stages for '{emailOrNickname}'. User not found.");
            return null;
        }

        private bool IsNotFoundError(Exception ex)
        {
            return ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
    }
}

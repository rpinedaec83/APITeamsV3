using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IGraphUserLookupService
    {
        /// <summary>
        /// Looks up a user in Microsoft Graph using a sequence of fallback strategies (UPN, Nickname, Alternative Domain, Legacy Domain).
        /// </summary>
        /// <param name="client">The pre-authenticated GraphServiceClient.</param>
        /// <param name="emailOrNickname">The primary email or nickname to search for.</param>
        /// <param name="altDomain">An optional alternative domain (from CompanyConfig) to try if primary fails.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The User object if found, or null.</returns>
        Task<User?> FindUserAsync(GraphServiceClient client, string emailOrNickname, string? altDomain = null, CancellationToken cancellationToken = default);
    }
}

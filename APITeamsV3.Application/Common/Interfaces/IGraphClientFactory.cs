using Microsoft.Graph;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IGraphClientFactory
    {
        Task<GraphServiceClient> CreateClientAsync();
    }
}

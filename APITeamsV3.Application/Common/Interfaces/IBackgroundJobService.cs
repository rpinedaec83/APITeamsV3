using System.Threading.Tasks;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IBackgroundJobService
    {
        Task<int> EnqueueJobAsync(string jobType, string targetId, string companyKey);
        Task<SyncJob?> GetJobAsync(int id);
    }
}

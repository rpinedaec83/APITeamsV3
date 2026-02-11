using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using System;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Services
{
    public class BackgroundJobService : IBackgroundJobService
    {
        private readonly CentralDbContext _context;

        public BackgroundJobService(CentralDbContext context)
        {
            _context = context;
        }

        public async Task<int> EnqueueJobAsync(string jobType, string targetId, string companyKey)
        {
            var job = new SyncJob
            {
                JobType = jobType,
                TargetId = targetId,
                CompanyKey = companyKey,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _context.SyncJobs.AddAsync(job);
            await _context.SaveChangesAsync();
            
            return job.Id;
        }

        public async Task<SyncJob?> GetJobAsync(int id)
        {
            return await _context.SyncJobs.FindAsync(id);
        }
    }
}

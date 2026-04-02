using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ICentralDbContext
    {
        DbSet<CompanyConfig> CompanyConfigs { get; }
        DbSet<CompanySede> CompanySedes { get; }
        DbSet<SyncSchedule> SyncSchedules { get; }
        DbSet<SyncJob> SyncJobs { get; }
        DbSet<CompanyPilotSection> CompanyPilotSections { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}

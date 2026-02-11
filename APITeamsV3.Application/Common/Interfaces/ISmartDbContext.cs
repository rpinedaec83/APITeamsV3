using Microsoft.EntityFrameworkCore;
using APITeamsV3.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ISmartDbContext
    {
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}

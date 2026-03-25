using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Infrastructure.Persistence.Repositories
{
    public class TeamsLogOperativoRepository : ITeamsLogOperativoRepository
    {
        private readonly ISmartDbContext _context;

        public TeamsLogOperativoRepository(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(TeamsLogOperativo log)
        {
            _context.Set<TeamsLogOperativo>().Add(log);
            await _context.SaveChangesAsync(System.Threading.CancellationToken.None);
        }
    }
}

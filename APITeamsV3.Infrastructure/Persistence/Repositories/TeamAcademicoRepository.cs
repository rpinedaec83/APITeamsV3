using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Infrastructure.Persistence.Repositories
{
    public class TeamAcademicoRepository : ITeamAcademicoRepository
    {
        private readonly ISmartDbContext _context;

        public TeamAcademicoRepository(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<TeamEntity?> GetBySeccionIdAsync(int idSeccionSmart)
        {
            // Buscamos priorizando el estado activo 'A' sobre inactivas 'I'
            return await _context.Set<TeamEntity>()
                                 .OrderBy(t => t.EstadoTeam) // 'A' asciende sobre 'I'
                                 .FirstOrDefaultAsync(t => t.IdSeccionSmart == idSeccionSmart);
        }
        
        public async Task<TeamEntity?> GetByIdTeamsAsync(string idTeams)
        {
            return await _context.Set<TeamEntity>()
                                 .FirstOrDefaultAsync(t => t.IdTeamsGroup == idTeams);
        }

        public async Task UpdateAsync(TeamEntity team)
        {
            _context.Set<TeamEntity>().Update(team);
            await _context.SaveChangesAsync(default);
        }

        public async Task AddAsync(TeamEntity team)
        {
            _context.Set<TeamEntity>().Add(team);
            await _context.SaveChangesAsync(default);
        }
    }
}

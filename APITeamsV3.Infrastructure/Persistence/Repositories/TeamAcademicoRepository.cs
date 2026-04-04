using System.Linq;
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
            // Priorizar equipo activo y, dentro de ellos, el registro mas reciente.
            return await _context.Set<TeamEntity>()
                                 .Where(t => t.IdSeccionSmart == idSeccionSmart)
                                 .OrderBy(t => t.EstadoTeam == "A" ? 0 : 1)
                                 .ThenByDescending(t => t.FechaModificacion ?? t.FechaCreacion)
                                 .ThenByDescending(t => t.FechaCreacion)
                                 .FirstOrDefaultAsync();
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

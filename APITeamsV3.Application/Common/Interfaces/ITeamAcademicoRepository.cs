using System.Threading.Tasks;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ITeamAcademicoRepository
    {
        Task<TeamEntity?> GetBySeccionIdAsync(int idSeccionSmart);
        Task<TeamEntity?> GetByIdTeamsAsync(string idTeams);
        Task UpdateAsync(TeamEntity team);
        Task AddAsync(TeamEntity team);
    }
}

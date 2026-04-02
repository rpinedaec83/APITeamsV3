using System.Threading.Tasks;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ITeamProvisioningService
    {
        Task<string> ProvisionTeamAsync(Seccion seccion);
        Task UpdateTeamAsync(Seccion seccion, bool updateMembers = true, bool updateOwners = true, bool updateAgendas = false);
    }
}

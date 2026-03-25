using System.Threading.Tasks;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ITeamsLogOperativoRepository
    {
        Task LogAsync(TeamsLogOperativo log);
    }
}

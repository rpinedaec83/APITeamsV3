using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface INamingService
    {
        string GetMailNickname(Seccion seccion);
        string GetDisplayName(Seccion seccion);
    }
}

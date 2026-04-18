using System.Security.Claims;
using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.API.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                               ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("preferred_username");

        public string? UserName => _httpContextAccessor.HttpContext?.User?.Identity?.Name 
                                ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("name");

        public int? UserIdInt 
        {
            get
            {
                // En este sistema legacy, los IDs son INT. 
                // Si el usuario es autenticado vía MSAL, usamos un ID de rango alto (técnico) 
                // para diferenciarlo de los procesos automáticos (ID=1).
                
                var user = _httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    // TODO: Futuro mapeo contra tabla de Administradores
                    return 888888; 
                }

                // Tareas de fondo / Hangfire usualmente no tienen HttpContext
                return null;
            }
        }
    }
}

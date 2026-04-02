using System;

namespace APITeamsV3.Domain.Entities
{
    public class AplicativoTeams
    {
        public int IdAplicativo { get; set; }
        public string? UsernameApp { get; set; }
        public string? PasswordApp { get; set; }
        public string? TenantId { get; set; }
        public string? AppClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Activo { get; set; }
        public int? IdSede { get; set; }
    }
}

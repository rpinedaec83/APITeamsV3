using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamEntity
    {
        public string IdTeamsGroup { get; set; } = string.Empty; // PK (Guid as string)
        public string Propietario1 { get; set; } = string.Empty; // Facilitator Email
        public string Propietario2 { get; set; } = string.Empty; // Service Account
        public string? Propietario3 { get; set; } // Facilitator Code? (nullable)
        public string? Propietario4 { get; set; } // New Facilitator Code/Owner (nullable)
        public string NombreTeam { get; set; } = string.Empty;
        public string DescripcionTeam { get; set; } = string.Empty;
        public string MailNickName { get; set; } = string.Empty;
        public string EstadoTeam { get; set; } = "A"; // A=Active, I=Inactive
        public int IdSeccionSmart { get; set; }
        public string IsActive { get; set; } = "A"; // Using string 'A'/'I'
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public int UsuarioCreacion { get; set; } = 99999999;
        public int? UsuarioModificacion { get; set; }

        // Navigation
        public Seccion? Seccion { get; set; }
    }
}

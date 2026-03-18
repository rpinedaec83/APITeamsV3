using System;

namespace APITeamsV3.Domain.Entities
{
    public class CompanySede
    {
        public int Id { get; set; }
        public int CompanyConfigId { get; set; }
        public int IdSede { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    }
}

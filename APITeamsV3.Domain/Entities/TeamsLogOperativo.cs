using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamsLogOperativo
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty; // e.g. "Error", "Info", "Warning"
        public string EntidadAfectada { get; set; } = string.Empty; // e.g. "Seccion", "Team", "Alumno"
        public string Referencia { get; set; } = string.Empty; // e.g. "12345" (Id de la entidad)
        public string Mensaje { get; set; } = string.Empty;
        public string ContextoTecnico { get; set; } = string.Empty; // StackTrace or JSON payload
        public string Severidad { get; set; } = string.Empty; // "High", "Medium", "Low"
        public string? JobId { get; set; } // Referencia al Job de Hangfire
        public string? Usuario { get; set; } // Nombre del usuario que ejecutó el proceso manual
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
    }
}

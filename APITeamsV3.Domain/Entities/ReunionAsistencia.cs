using System;
using System.Collections.Generic;

namespace APITeamsV3.Domain.Entities
{
    public class ReunionAsistencia
    {
        public int Id { get; set; }
        public int IdSeccion { get; set; }
        public string? IdTeamsGroup { get; set; }
        public string MeetingId { get; set; } = string.Empty;
        public string MeetingReportId { get; set; } = string.Empty;
        public DateTime MeetingStartDateTime { get; set; }
        public DateTime MeetingEndDateTime { get; set; }
        public int TotalParticipantCount { get; set; }
        public DateTime FechaSincronizacion { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<ReunionAsistenciaDetalle> Detalles { get; set; } = new List<ReunionAsistenciaDetalle>();
    }
}

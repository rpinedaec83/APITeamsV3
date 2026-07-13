using System;

namespace APITeamsV3.Domain.Entities
{
    public class ReunionAsistenciaIntervalo
    {
        public int Id { get; set; }
        public int IdReunionAsistenciaDetalle { get; set; }
        public DateTime JoinDateTime { get; set; }
        public DateTime LeaveDateTime { get; set; }
        public int DurationInSeconds { get; set; }

        // Navigation property
        public ReunionAsistenciaDetalle? ReunionAsistenciaDetalle { get; set; }
    }
}

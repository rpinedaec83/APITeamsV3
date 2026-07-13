using System;

namespace APITeamsV3.Domain.Entities
{
    public class ReunionAsistenciaDetalle
    {
        public int Id { get; set; }
        public int IdReunionAsistencia { get; set; }
        public string? EmailAddress { get; set; }
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
        public int TotalAttendanceInSeconds { get; set; }
        public DateTime? FirstJoinDateTime { get; set; }
        public DateTime? LastLeaveDateTime { get; set; }

        // Navigation property
        public ReunionAsistencia? ReunionAsistencia { get; set; }
        public ICollection<ReunionAsistenciaIntervalo> Intervalos { get; set; } = new List<ReunionAsistenciaIntervalo>();
    }
}

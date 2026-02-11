using System;

namespace APITeamsV3.Domain.Entities
{
    public class Alumno
    {
        public int IdAlumno { get; set; }
        public string Codigo { get; set; } = string.Empty; // CodigoReal or Id
        public string EmailInstitucion { get; set; } = string.Empty;
        public string? EmailPersonal { get; set; }
        public string Nombre { get; set; } = string.Empty;
        // Add other fields as necessary from legacy DB
    }
}

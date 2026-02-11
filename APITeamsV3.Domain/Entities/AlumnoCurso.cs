using System;

namespace APITeamsV3.Domain.Entities
{
    public class AlumnoCurso
    {
        public int IdAlumnoCurso { get; set; }
        public int IdSeccion { get; set; }
        public int IdAlumno { get; set; }
        public int IdMatricula { get; set; }
        public string Estado { get; set; } = string.Empty;
        public bool EsMatricula { get; set; } // 1 = Active (BIT in DB)
        
        // Navigation
        public Alumno? Alumno { get; set; }
        public Seccion? Seccion { get; set; }
    }
}

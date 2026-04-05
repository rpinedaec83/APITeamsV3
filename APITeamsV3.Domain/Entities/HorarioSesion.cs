using System;
using System.ComponentModel.DataAnnotations;

namespace APITeamsV3.Domain.Entities
{
    public class HorarioSesion
    {
        public int IdSeccion { get; set; }
        public int IdHorario { get; set; }
        public int Numero { get; set; }
        public DateTime Fecha { get; set; }
        public int Inicio { get; set; }
        public int Fin { get; set; }
        
        // Additional properties that might be useful based on SQL usage
        public int? IdActorProgramado { get; set; }
        public int? IdActorReemplazo { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}

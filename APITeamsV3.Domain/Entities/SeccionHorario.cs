using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APITeamsV3.Domain.Entities
{
    public class SeccionHorario
    {
        [Key]
        public int IdSeccion { get; set; }
        public string? UrlClaseVirtual { get; set; }
        public string? IdEvento { get; set; }
    }
}

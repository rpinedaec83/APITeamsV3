using System.ComponentModel.DataAnnotations;

namespace APITeamsV3.Domain.Entities
{
    public class Facilitador
    {
        [Key]
        public int IdFacilitador { get; set; }
        public string CodigoAnterior { get; set; } = string.Empty;
        public string EmailInstitucion { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace APITeamsV3.Domain.Entities
{
    public class Facilitador
    {
        [Key]
        public int IdFacilitador { get; set; }
        public string CodigoAnterior { get; set; }
        public string EmailInstitucion { get; set; }
    }
}

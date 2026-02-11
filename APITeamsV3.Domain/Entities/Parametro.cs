namespace APITeamsV3.Domain.Entities
{
    public class Parametro
    {
        public int IdParametro { get; set; } // Assuming PK exists
        public string Nombre { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
        public string Valor2 { get; set; } = string.Empty;
        public string Valor3 { get; set; } = string.Empty;
    }
}

namespace APITeamsV3.Domain.Entities
{
    /// <summary>
    /// Temporary mapping DTO for raw SQL results from Sede table in Smart DB.
    /// </summary>
    public class SmartSedeImport
    {
        public int IdSede { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }
}

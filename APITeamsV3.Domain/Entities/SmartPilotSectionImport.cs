namespace APITeamsV3.Domain.Entities
{
    /// <summary>
    /// Temporary mapping DTO for raw SQL results from Seccion/Periodo query in Smart DB.
    /// Used to fetch pilot candidate sections by period code.
    /// </summary>
    public class SmartPilotSectionImport
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
    }
}

namespace APITeamsV3.Domain.Entities
{
    /// <summary>
    /// Temporary mapping DTO for raw SQL results from Periodo query in Smart DB.
    /// Used to fetch available pilot period codes.
    /// </summary>
    public class SmartPilotPeriodImport
    {
        public string Codigo { get; set; } = string.Empty;
    }
}

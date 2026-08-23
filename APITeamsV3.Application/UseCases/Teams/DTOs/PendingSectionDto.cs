namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    public class PendingSectionDto
    {
        public int IdSeccion { get; set; }
        public string NombreCurso { get; set; } = string.Empty;
        public string CodigoSeccion { get; set; } = string.Empty;
        public string Sede { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string EmailFacilitador { get; set; } = string.Empty;
        public string NombreFacilitador { get; set; } = string.Empty;
        public bool HasMetadata { get; set; }
    }
}

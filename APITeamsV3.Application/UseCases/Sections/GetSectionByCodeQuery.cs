using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionByCodeQuery : IRequest<SectionDetailDto>
    {
        public string Code { get; set; } = string.Empty;
        public string? Periodo { get; set; }
    }

    public class SectionDetailDto
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Sede { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string Curso { get; set; } = string.Empty;
        public string Profesor { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Semestre { get; set; } = string.Empty;
        public string UnidadNegocio { get; set; } = string.Empty;
        public List<StudentSummaryDto> Members { get; set; } = new();
        public bool HasTeam { get; set; }
        public bool EsTeams { get; set; }
        public string? IneligibilityReason { get; set; }
    }

    public class StudentSummaryDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
    }
}

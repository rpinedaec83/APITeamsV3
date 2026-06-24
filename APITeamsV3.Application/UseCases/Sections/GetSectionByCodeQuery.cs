using MediatR;
using System.Collections.Generic;
using System;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionByCodeQuery : IRequest<SectionDetailDto>
    {
        public string Code { get; set; } = string.Empty;
        public string? Periodo { get; set; }
        public bool SkipSharePoint { get; set; }
    }

    public class SectionDetailDto
    {
        public int IdSeccion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Sede { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string Curso { get; set; } = string.Empty;
        public string Profesor { get; set; } = string.Empty;
        public string? TeamTeacher { get; set; }
        public string? ReplacementTeacher { get; set; }
        public string? TeacherReplacementStatus { get; set; }
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string PromocionCodigo { get; set; } = string.Empty;
        public string PromocionNombre { get; set; } = string.Empty;
        public string Semestre { get; set; } = string.Empty;
        public string UnidadNegocio { get; set; } = string.Empty;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? LinkGrabacion { get; set; }
        public string? LinkSharePoint { get; set; }
        public long? SharePointTotalBytes { get; set; }
        public long? SharePointUsedBytes { get; set; }
        public long? SharePointRemainingBytes { get; set; }
        public double? SharePointPercentAvailable { get; set; }
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

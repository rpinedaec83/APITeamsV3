using System;

namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    public class NextSessionDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string EmailAppTeam { get; set; } = string.Empty;
        public int IdHorario { get; set; }
        public string Codigo { get; set; } = string.Empty; // Section Code
        public int Numero { get; set; } // Meeting Number
        public DateTime Fecha { get; set; }
        public int Inicio { get; set; }
        public int Fin { get; set; }
        public int IdCurso { get; set; }
        public string NombreCompleto { get; set; } = string.Empty; // Actor Name (Programmed/Replacer)
        public string CodigoAnterior { get; set; } = string.Empty; // Facilitator Code
        public string EmailInstitucion { get; set; } = string.Empty; // Facilitator Email
        public string NombresFacilitador { get; set; } = string.Empty; // From Programacion
        public string EmailFacilitador { get; set; } = string.Empty; // From Programacion
        public string Content { get; set; } = string.Empty; // Resumen
        public string SubjectMeet { get; set; } = string.Empty; // NombreTeam
        public string DescripcionTeam { get; set; } = string.Empty;
        public string MailNickName { get; set; } = string.Empty;
        public string IsActive { get; set; } = string.Empty;
        public int IdUnidadNegocio { get; set; }
        public string? CuentaCarreras { get; set; }
        public string? CuentaExtension { get; set; }
    }
}

using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Students
{
    public class GetStudentByCodeQuery : IRequest<StudentDetailDto?>
    {
        public string Code { get; set; } = string.Empty;
    }

    public class StudentDetailDto
    {
        public int IdAlumno { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // Context info from their main enrollment
        public string Sede { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Semestre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty; // Main section?
        
        public List<StudentEnrollmentDto> EnrolledSections { get; set; } = new();
    }

    public class StudentEnrollmentDto
    {
        public int SectionId { get; set; }
        public string SectionCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string TeamStatus { get; set; } = "No Creado"; // Activo, No Creado
        public string StudentStatus { get; set; } = "Sin Team"; // En Team, Pendiente, Sin Team
    }
}

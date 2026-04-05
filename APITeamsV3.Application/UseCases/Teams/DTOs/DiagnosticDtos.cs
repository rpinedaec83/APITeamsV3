namespace APITeamsV3.Application.UseCases.Teams.DTOs
{
    public class CourseMissingFacilitatorDto
    {
        public string IdTeam { get; set; } = string.Empty;
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string ApellidosFacilitador { get; set; } = string.Empty;
    }

    public class MissingStudentDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string CodigoAlumno { get; set; } = string.Empty;
        public string NombresAlumno { get; set; } = string.Empty;
        public string ApellidosAlumno { get; set; } = string.Empty;
        public string EmailAlumno { get; set; } = string.Empty;
    }

    public class ObsoleteStudentDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string CodigoAlumno { get; set; } = string.Empty;
        public string EmailAlumno { get; set; } = string.Empty;
    }

    public class ExpiredTeamDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
    }

    public class RenamedTeamDto
    {
        public string IdTeamsGroup { get; set; } = string.Empty;
        public string NombreTeam { get; set; } = string.Empty;
        public string DescripcionTeam { get; set; } = string.Empty;
    }
}

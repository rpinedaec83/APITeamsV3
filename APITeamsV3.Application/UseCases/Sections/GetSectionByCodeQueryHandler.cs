using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionByCodeQueryHandler : IRequestHandler<GetSectionByCodeQuery, SectionDetailDto?>
    {
        private readonly ISmartDbContext _context;

        public GetSectionByCodeQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<SectionDetailDto?> Handle(GetSectionByCodeQuery request, CancellationToken cancellationToken)
        {
            // 1. Fetch Section Details from View
            // The view likely contains all needed info except list of students
            var section = await _context.Set<Seccion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GrupoCodigo == request.Code || s.Codigo == request.Code, cancellationToken);

            if (section == null) return null;

            // 2. Check if Team exists
            var team = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == section.IdSeccion && t.EstadoTeam == "A", cancellationToken);

            // 3. Fetch Students (if needed for the response)
            // The legacy SP inserts into TeamsProgramacionAlumnos. We can query AlumnoCurso directly.
            // Assuming AlumnoCurso.Estado = 'A' means active
            // 3. Status Logic
            var memberStatusMap = new Dictionary<string, string>();
            if (team != null)
            {
                var teamMembers = await _context.Set<TeamMember>()
                    .AsNoTracking()
                    .Where(tm => tm.IdTeams == team.IdTeamsGroup && tm.Estado == "A")
                    .Select(tm => tm.CodigoAlumno)
                    .ToListAsync(cancellationToken);
                
                foreach (var code in teamMembers)
                {
                    if (!string.IsNullOrEmpty(code)) memberStatusMap[code] = "En Team";
                }
            }

            // 4. Fetch Students and Map Status
            var students = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == section.IdSeccion && ac.EsMatricula)
                .Select(ac => new StudentSummaryDto 
                {
                    Code = ac.Alumno != null ? ac.Alumno.Codigo : "N/A",
                    Name = ac.Alumno != null ? ac.Alumno.Nombre : "Unknown",
                    Status = "Matriculado" // Default
                })
                .ToListAsync(cancellationToken);

            // Apply calculated status
            foreach (var s in students)
            {
                if (team == null)
                {
                    s.Status = "Sin Team";
                }
                else if (memberStatusMap.ContainsKey(s.Code))
                {
                    s.Status = "En Team";
                }
                else
                {
                    s.Status = "Pendiente";
                }
            }

            return new SectionDetailDto
            {
                IdSeccion = section.IdSeccion,
                Codigo = section.GrupoCodigo, // or section.Codigo
                Sede = section.SedeNombre,
                Producto = section.ProductoNombre,
                Curso = section.CursoNombre,
                Profesor = $"{section.NombresFacilitador} ({section.CodigoFacilitador})",
                Division = section.UnidadAcademicaNombre, // Mapping might vary
                Programa = section.UnidadNegocioNombre, // Mapping might vary
                Semestre = section.CodigoPeriodo, // or Semestre
                UnidadNegocio = section.UnidadNegocioNombre,
                Members = students,
                HasTeam = team != null,
                EsTeams = section.EsTeams
            };
        }
    }
}

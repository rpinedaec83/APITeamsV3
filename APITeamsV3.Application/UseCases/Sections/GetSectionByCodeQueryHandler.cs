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
            // 1. Fetch Section Details from View (vw_MatriculasActivas)
            var section = await _context.Set<Seccion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GrupoCodigo == request.Code || s.Codigo == request.Code, cancellationToken);

            if (section == null)
            {
                // Fallback: Search in Physical Seccion Table if not in Active view
                var sectionTable = await _context.Set<SeccionTable>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Codigo == request.Code, cancellationToken);
                
                if (sectionTable == null) return null;

                // Create a lightweight section object from the table data
                section = new Seccion
                {
                    IdSeccion = sectionTable.IdSeccion,
                    Codigo = sectionTable.Codigo,
                    GrupoCodigo = sectionTable.Codigo, // Use same for display if no group code available
                    CursoNombre = "Información limitada (No está en vista activa)",
                    EsTeams = false // Assuming no team if not active
                };
            }

            // 2. Check if Team exists
            var team = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == section.IdSeccion && t.EstadoTeam == "A", cancellationToken);

            // 3. Member Status Logic
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

            // 4. Fetch Students and Map Status using EF Core (vw_AlumnoMaster)
            var students = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == section.IdSeccion && ac.EsMatricula)
                .Select(ac => new StudentSummaryDto 
                {
                    // Alumno view should provide the basic info
                    Code = ac.Alumno != null ? ac.Alumno.Codigo : "N/A",
                    Name = ac.Alumno != null ? ac.Alumno.Nombre : "Unknown",
                    Status = team == null ? "Sin Team" : (memberStatusMap.ContainsKey(ac.Alumno != null ? ac.Alumno.Codigo : "") ? "En Team" : "Pendiente")
                })
                .ToListAsync(cancellationToken);

            return new SectionDetailDto
            {
                IdSeccion = section.IdSeccion,
                Codigo = section.GrupoCodigo ?? section.Codigo,
                Sede = section.SedeNombre,
                Producto = section.ProductoNombre,
                Curso = section.CursoNombre,
                Profesor = $"{section.NombresFacilitador} ({section.CodigoFacilitador})",
                Division = section.UnidadAcademicaNombre,
                Programa = section.UnidadNegocioNombre,
                Semestre = section.CodigoPeriodo,
                UnidadNegocio = section.UnidadNegocioNombre,
                Members = students,
                HasTeam = team != null,
                EsTeams = section.EsTeams
            };
        }
    }
}

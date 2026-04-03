using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Sections
{
    public class GetSectionByCodeQueryHandler : IRequestHandler<GetSectionByCodeQuery, SectionDetailDto?>
    {
        private readonly ISmartDbContext _context;
        private readonly ISectionEligibilityService _eligibilityService;
        private readonly ITenantProvider _tenantProvider;

        public GetSectionByCodeQueryHandler(ISmartDbContext context, ISectionEligibilityService eligibilityService, ITenantProvider tenantProvider)
        {
            _context = context;
            _eligibilityService = eligibilityService;
            _tenantProvider = tenantProvider;
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

            // 2. Check Eligibility Reason
            var tenant = _tenantProvider.GetCurrentTenant();
            var eligibility = await _eligibilityService.IsEligibleForTeamsAsync(section, tenant.CompanyKey);

            // 3. Check if Team exists
            var team = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == section.IdSeccion && t.EstadoTeam == "A", cancellationToken);

            // 4. Member Status Logic
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

            // 5. Fetch Students and Map Status using EF Core (vw_AlumnoMaster)
            var students = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == section.IdSeccion && ac.EsMatricula)
                .Select(ac => new StudentSummaryDto 
                {
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
                EsTeams = eligibility.IsEligible,
                IneligibilityReason = eligibility.IsEligible ? null : eligibility.Reason
            };
        }
    }
}

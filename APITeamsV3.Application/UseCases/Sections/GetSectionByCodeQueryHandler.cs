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
                    .Where(s => s.Codigo == request.Code)
                    .Select(s => new SeccionTable
                    {
                        IdSeccion = s.IdSeccion,
                        Codigo = s.Codigo,
                        FechaInicio = s.FechaInicio,
                        FechaFin = s.FechaFin
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (sectionTable == null) return null;

                // Create a lightweight section object from the table data
                section = new Seccion
                {
                    IdSeccion = sectionTable.IdSeccion,
                    Codigo = sectionTable.Codigo,
                    GrupoCodigo = sectionTable.Codigo, // Use same for display if no group code available
                    FechaInicio = sectionTable.FechaInicio ?? default,
                    FechaFin = sectionTable.FechaFin ?? default,
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

            var sectionJoinUrl = await _context.Set<SeccionHorario>()
                .AsNoTracking()
                .Where(sh => sh.IdSeccion == section.IdSeccion)
                .Select(sh => sh.UrlClaseVirtual)
                .FirstOrDefaultAsync(cancellationToken);

            var latestSessionJoinUrl = await _context.Set<TeamSession>()
                .AsNoTracking()
                .Where(th => th.IdCurso == section.IdSeccion && th.Estado == "A" && th.JoinUrl != null && th.JoinUrl != "")
                .OrderByDescending(th => th.Fecha)
                .ThenByDescending(th => th.Inicio)
                .Select(th => th.JoinUrl)
                .FirstOrDefaultAsync(cancellationToken);

            var linkGrabacion = !string.IsNullOrWhiteSpace(sectionJoinUrl)
                ? sectionJoinUrl
                : latestSessionJoinUrl;

            // 4. Member Status Logic
            var memberIdentifiers = new HashSet<string>();
            if (team != null)
            {
                var teamMembers = await _context.Set<TeamMember>()
                    .AsNoTracking()
                    .Where(tm => tm.IdTeams == team.IdTeamsGroup && tm.Estado == "A")
                    .Select(tm => new { tm.CodigoAlumno, tm.Email })
                    .ToListAsync(cancellationToken);
                
                foreach (var member in teamMembers)
                {
                    AddIdentifierIfPresent(memberIdentifiers, member.CodigoAlumno);
                    AddIdentifierIfPresent(memberIdentifiers, member.Email);
                }
            }

            // 5. Fetch Students and Map Status using EF Core (vw_AlumnoMaster)
            var enrolledStudents = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Alumno)
                .Where(ac => ac.IdSeccion == section.IdSeccion && ac.EsMatricula)
                .Select(ac => new
                {
                    Code = ac.Alumno != null ? ac.Alumno.Codigo : "N/A",
                    Name = ac.Alumno != null ? ac.Alumno.Nombre : "Unknown",
                    EmailInstitucion = ac.Alumno != null ? ac.Alumno.EmailInstitucion : string.Empty,
                    EmailPersonal = ac.Alumno != null ? ac.Alumno.EmailPersonal : null
                })
                .ToListAsync(cancellationToken);

            var students = enrolledStudents
                .Select(student => new StudentSummaryDto
                {
                    Code = student.Code,
                    Name = student.Name,
                    Status = team == null
                        ? "Sin Team"
                        : BuildStudentIdentifiers(student.Code, student.EmailInstitucion, student.EmailPersonal).Any(memberIdentifiers.Contains)
                            ? "En Team"
                            : "Pendiente"
                })
                .ToList();

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
                FechaInicio = section.FechaInicio == default ? null : section.FechaInicio,
                FechaFin = section.FechaFin == default ? null : section.FechaFin,
                LinkGrabacion = linkGrabacion,
                Members = students,
                HasTeam = team != null,
                EsTeams = eligibility.IsEligible,
                IneligibilityReason = eligibility.IsEligible ? null : eligibility.Reason
            };
        }

        private static IEnumerable<string> BuildStudentIdentifiers(string? code, string? institutionalEmail, string? personalEmail)
        {
            var identifiers = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            AddIdentifierIfPresent(identifiers, code);
            AddIdentifierIfPresent(identifiers, institutionalEmail);
            AddIdentifierIfPresent(identifiers, personalEmail);
            return identifiers;
        }

        private static void AddIdentifierIfPresent(ISet<string> identifiers, string? value)
        {
            var normalized = NormalizeIdentifier(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                identifiers.Add(normalized);
            }
        }

        private static string NormalizeIdentifier(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var at = trimmed.IndexOf('@');
            return (at >= 0 ? trimmed.Substring(0, at) : trimmed).Trim().ToLowerInvariant();
        }
    }
}

using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Students
{
    public class GetStudentByCodeQueryHandler : IRequestHandler<GetStudentByCodeQuery, StudentDetailDto?>
    {
        private readonly ISmartDbContext _context;

        public GetStudentByCodeQueryHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<StudentDetailDto?> Handle(GetStudentByCodeQuery request, CancellationToken cancellationToken)
        {
            var student = await _context.Set<Alumno>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Codigo == request.Code, cancellationToken); // Removed CodigoAnterior check as it's not in view

            if (student == null) return null;

            // 1. Get EsTeams parameter (Legacy Logic)
            int dias = 14;
            var parametro = await _context.Set<Parametro>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Nombre == "EsTeams", cancellationToken);
            
            if (parametro != null && int.TryParse(parametro.Valor, out int val))
            {
                dias = val;
            }

            // 2. Fetch all active enrollments
            var allEnrollments = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Seccion)
                .Where(ac => ac.IdAlumno == student.IdAlumno && (ac.Estado == "A" || ac.EsMatricula))
                .ToListAsync(cancellationToken);

            // 3. Filter by Date (Legacy Logic mimic)
            var validEnrollments = new List<AlumnoCurso>();
            var today = DateTime.Now.Date;

            foreach (var enrollment in allEnrollments)
            {
                var section = enrollment.Seccion;
                if (section == null) continue;

                bool isValid = false;

                if (section.TipoServicio == "P" || section.TipoServicio == "L")
                {
                    // BETWEEN Start-Dias AND End+Dias
                    var start = section.FechaInicio.AddDays(-dias);
                    var end = section.FechaFin.AddDays(dias);
                    if (today >= start && today <= end) isValid = true;
                }
                else if (section.TipoServicio == "C")
                {
                    if (section.PeriodoInicio.HasValue && section.PeriodoFin.HasValue)
                    {
                        var start = section.PeriodoInicio.Value.AddDays(-dias);
                        var end = section.PeriodoFin.Value.AddDays(dias);
                        if (today >= start && today <= end) isValid = true;
                    }
                }
                
                if (isValid)
                {
                    validEnrollments.Add(enrollment);
                }
            }

            // Fetch Teams for these sections
            var sectionIds = validEnrollments.Select(e => e.IdSeccion).Distinct().ToList();
            var teams = await _context.Set<TeamEntity>()
                .AsNoTracking()
                .Where(t => sectionIds.Contains(t.IdSeccionSmart) && t.EstadoTeam == "A")
                .ToListAsync(cancellationToken);
            
            // Fetch Membership in these teams
            var teamIds = teams.Select(t => t.IdTeamsGroup).ToList();
            var memberships = await _context.Set<TeamMember>()
                .AsNoTracking()
                .Where(tm => teamIds.Contains(tm.IdTeams) && tm.CodigoAlumno == student.Codigo && tm.Estado == "A") // Use Codigo
                .ToListAsync(cancellationToken);

            var enrolledDtos = new List<StudentEnrollmentDto>();

            var mainEnrollment = validEnrollments.FirstOrDefault();
            var sectionInfo = mainEnrollment?.Seccion;

            foreach (var enrollment in validEnrollments)
            {
                if (enrollment.Seccion == null) continue;

                var team = teams.FirstOrDefault(t => t.IdSeccionSmart == enrollment.IdSeccion);
                var enrollmentDto = new StudentEnrollmentDto
                {
                    SectionId = enrollment.IdSeccion,
                    SectionCode = enrollment.Seccion.GrupoCodigo,
                    CourseName = enrollment.Seccion.CursoNombre,
                    TeamStatus = team != null ? "Activo" : "No Creado"
                };

                if (team == null)
                {
                    enrollmentDto.StudentStatus = "Sin Team";
                }
                else
                {
                    var isMember = memberships.Any(m => m.IdTeams == team.IdTeamsGroup);
                    enrollmentDto.StudentStatus = isMember ? "En Team" : "Pendiente";
                }

                enrolledDtos.Add(enrollmentDto);
            }

            return new StudentDetailDto
            {
                IdAlumno = student.IdAlumno,
                Codigo = student.Codigo, // Use Codigo
                Nombre = student.Nombre, // Or split names if needed
                Email = student.EmailInstitucion,
                Sede = sectionInfo?.SedeNombre ?? "N/A",
                Producto = sectionInfo?.ProductoNombre ?? "N/A",
                Division = sectionInfo?.UnidadAcademicaNombre ?? "N/A",
                Programa = sectionInfo?.UnidadNegocioNombre ?? "N/A",
                Semestre = sectionInfo?.CodigoPeriodo ?? "N/A",
                Seccion = sectionInfo?.GrupoCodigo ?? "N/A",
                EnrolledSections = enrolledDtos
            };
        }
    }
}

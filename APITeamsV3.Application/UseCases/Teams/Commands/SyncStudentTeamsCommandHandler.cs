using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncStudentTeamsCommandHandler : IRequestHandler<SyncStudentTeamsCommand, List<string>>
    {
        private readonly ISmartDbContext _context;
        private readonly IHangfireJobService _jobService;
        private readonly ILogger<SyncStudentTeamsCommandHandler> _logger;

        public SyncStudentTeamsCommandHandler(
            ISmartDbContext context, 
            IHangfireJobService jobService,
            ILogger<SyncStudentTeamsCommandHandler> logger)
        {
            _context = context;
            _jobService = jobService;
            _logger = logger;
        }

        public async Task<List<string>> Handle(SyncStudentTeamsCommand request, CancellationToken cancellationToken)
        {
            var jobIds = new List<string>();

            var student = await _context.Set<Alumno>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Codigo == request.CodigoAlumno, cancellationToken);
                
            if (student == null)
            {
                _logger.LogWarning($"Student {request.CodigoAlumno} not found.");
                return jobIds;
            }

            int dias = 14;
            var parametro = await _context.Set<Parametro>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Nombre == "EsTeams", cancellationToken);
                
            if (parametro != null && int.TryParse(parametro.Valor, out int val))
            {
                dias = val;
            }

            var allEnrollments = await _context.Set<AlumnoCurso>()
                .AsNoTracking()
                .Include(ac => ac.Seccion)
                .Where(ac => ac.IdAlumno == student.IdAlumno && (ac.Estado == "A" || ac.EsMatricula))
                .ToListAsync(cancellationToken);

            var today = DateTime.Now.Date;
            var validSectionIds = new List<int>();

            foreach (var enrollment in allEnrollments)
            {
                var section = enrollment.Seccion;
                if (section == null) continue;

                bool isValid = false;

                if (section.TipoServicio == "P" || section.TipoServicio == "L")
                {
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
                    validSectionIds.Add(section.IdSeccion);
                }
            }

            // Evitar duplicados
            validSectionIds = validSectionIds.Distinct().ToList();

            foreach (var sectionId in validSectionIds)
            {
                // Flujo 5: encolar actualización por cada sección
                var jobId = _jobService.EnqueueSyncSectionTeam(sectionId);
                jobIds.Add(jobId);
                _logger.LogInformation($"Enqueued SyncSectionTeam (Job {jobId}) for Section {sectionId} triggered by Student {request.CodigoAlumno}");
            }

            return jobIds;
        }
    }
}

using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncObsoleteStudentsCommandHandler : IRequestHandler<SyncObsoleteStudentsCommand, List<ObsoleteStudentDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncObsoleteStudentsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<ObsoleteStudentDto>> Handle(SyncObsoleteStudentsCommand request, CancellationToken cancellationToken)
        {
            // Lógica para encontrar alumnos que existen en TeamsUsuarios 
            // pero que ya no están en TeamsProgramacionAlumnos (se retiraron).
            
            var obsoleteStudents = await (from tu in _context.TeamsUsuarios
                                          join te in _context.TeamsEquipos on tu.IdTeams equals te.IdTeamsGroup
                                          where te.IdSeccionSmart == request.IdSeccion
                                             && tu.Estado == "A"
                                             && tu.Tipo == "A"
                                             && te.EstadoTeam == "A"
                                             && !_context.TeamsProgramacionAlumnos
                                                    .Any(mpa => mpa.IdCurso == te.IdSeccionSmart 
                                                             && mpa.CodigoAlumno == tu.CodigoAlumno)
                                          select new ObsoleteStudentDto
                                          {
                                              IdTeamsGroup = te.IdTeamsGroup,
                                              CodigoAlumno = tu.CodigoAlumno
                                          })
                                          .ToListAsync(cancellationToken);

            return obsoleteStudents;
        }
    }
}

using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
            // Option 5/30 Logic: Find students that exist in TeamsUsuarios 
            // but are no longer in TeamsProgramacionAlumnos (dropped enrollment).
            // Returns the list so the caller (Graph API client) can remove them from Teams.

            var sql = @"
                WITH dtOldMembers AS (
                  SELECT TE.IdTeamsGroup,
                    TU.CodigoAlumno
                  FROM TeamsUsuarios TU WITH (NOLOCK)
                    LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)
                  WHERE TE.IdSeccionSmart = {0}
                    AND NOT EXISTS (
                      SELECT 1
                      FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)
                      WHERE MPG.IdCurso = TE.IdSeccionSmart
                        AND MPG.CodigoAlumno = TU.CodigoAlumno
                    )
                    AND TU.Estado = 'A'
                    AND TU.Tipo = 'A'
                    AND TE.EstadoTeam = 'A'
                )
                SELECT *
                FROM dtOldMembers
                WHERE ISNULL(IdTeamsGroup, '') <> ''";

            return await _context.Database.SqlQueryRaw<ObsoleteStudentDto>(sql, request.IdSeccion).ToListAsync(cancellationToken);
        }
    }
}

using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncRenamedTeamsCommandHandler : IRequestHandler<SyncRenamedTeamsCommand, List<RenamedTeamDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncRenamedTeamsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<RenamedTeamDto>> Handle(SyncRenamedTeamsCommand request, CancellationToken cancellationToken)
        {
            // 1. Obtener la sección y calcular el nombre deseado
            // Nota: En un sistema real, esto coincidiría con INamingService.
            // Aquí replicamos la lógica del SP sp_GetRenamedTeams.
            
            var query = from mpg in _context.TeamsProgramacionAlumnos // Usamos Alumnos para obtener los datos de la sección
                        join es in _context.EmpresaSedeParametro on mpg.IdSede equals es.IdSede
                        join s in _context.SeccionTable on mpg.IdCurso equals s.IdSeccion
                        where mpg.IdCurso == request.IdSeccion 
                           && es.Nombre == "PROPIETARIOTINA"
                           && mpg.IdUnidadNegocio.ToString() == es.Valor3
                        select new 
                        {
                            mpg.IdCurso,
                            DesiredName = mpg.NombreCurso + " [" + mpg.NombreProducto + "][" + s.Codigo + "]",
                            DesiredDescription = "SEDE: " + mpg.NombreSede + " --> DIVISION: " + mpg.NombreUnidadNegocio + " --> PROGRAMA: " + mpg.NombreUnidadAcademica + "-" + mpg.CodigoPeriodo + " --> PRODUCTO: " + mpg.NombreProducto + " --> SEMESTRE: " + mpg.Semestre + " --> SECCION: " + mpg.GrupoCodigo + " --> CURSO: " + (mpg.NombreCurso.Length > 20 ? mpg.NombreCurso.Substring(0, 20) : mpg.NombreCurso) + " - " + mpg.IdCurso + " --> PROFESOR: " + mpg.CodigoFacilitador + " - " + mpg.NombresFacilitador
                        };

            var desired = await query.Distinct().FirstOrDefaultAsync(cancellationToken);
            if (desired == null) return new List<RenamedTeamDto>();

            // 2. Comparar con el TeamEntity actual
            var existingTeams = await _context.TeamsEquipos
                .Where(te => te.IdSeccionSmart == request.IdSeccion && te.EstadoTeam == "A")
                .ToListAsync(cancellationToken);

            var renamed = new List<RenamedTeamDto>();

            foreach (var team in existingTeams)
            {
                // Limitar descripción a 250 caracteres como hace el SP
                string finalDescription = desired.DesiredDescription.Length > 250 
                    ? desired.DesiredDescription.Substring(0, 250) 
                    : desired.DesiredDescription;

                if (team.NombreTeam != desired.DesiredName || team.DescripcionTeam != finalDescription)
                {
                    renamed.Add(new RenamedTeamDto
                    {
                        IdTeamsGroup = team.IdTeamsGroup,
                        NombreTeam = desired.DesiredName,
                        DescripcionTeam = finalDescription
                    });
                }
            }

            return renamed;
        }
    }
}

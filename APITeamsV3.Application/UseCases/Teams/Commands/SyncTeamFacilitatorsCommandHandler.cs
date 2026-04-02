using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncTeamFacilitatorsCommandHandler : IRequestHandler<SyncTeamFacilitatorsCommand, List<TeamFacilitatorChangeDto>>
    {
        private readonly ISmartDbContext _context;

        public SyncTeamFacilitatorsCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<TeamFacilitatorChangeDto>> Handle(SyncTeamFacilitatorsCommand request, CancellationToken cancellationToken)
        {
            // Paso 1: Obtener el Propietario4 (Owner por defecto según sede/unidad de negocio)
            var p4 = await _context.TeamsProgramacionGeneral
                .Where(m => m.IdCurso == request.IdSeccion)
                .Join(_context.EmpresaSedeParametro,
                    m => m.IdSede,
                    es => es.IdSede,
                    (m, es) => new { m, es })
                .Where(x => x.es.Nombre == "PROPIETARIOTINA" && x.m.IdUnidadNegocio.ToString() == x.es.Valor3)
                .Select(x => x.es.Valor)
                .FirstOrDefaultAsync(cancellationToken);

            // Paso 2: Actualización Masiva (P3 a NULL, P4 al valor encontrado)
            await _context.TeamsEquipos
                .Where(t => t.IdSeccionSmart == request.IdSeccion && t.Propietario4 == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Propietario3, (string?)null)
                    .SetProperty(t => t.Propietario4, p4),
                    cancellationToken);

            // Paso 3: Retornar los cambios (mimic de dtFacilitadores CTE)
            // Buscamos facilitadores en ProgramacionAlumnos que no sean ya P3 en TeamsEquipos activos
            var result = await _context.TeamsProgramacionAlumnos
                .Where(mpg => mpg.IdCurso == request.IdSeccion)
                .Join(_context.TeamsEquipos.Where(te => te.EstadoTeam == "A"),
                    mpg => mpg.IdCurso,
                    te => te.IdSeccionSmart,
                    (mpg, te) => new { mpg, te })
                .Where(x => !_context.TeamsEquipos
                    .Any(t => t.IdSeccionSmart == x.mpg.IdCurso && t.Propietario3 == x.mpg.EmailFacilitador && t.EstadoTeam == "A"))
                .Where(x => x.te.Propietario3 != null)
                .Select(x => new TeamFacilitatorChangeDto
                {
                    IdTeam = x.te.IdTeamsGroup,
                    CodigoFacilitador = x.mpg.CodigoFacilitador ?? string.Empty,
                    NombresFacilitador = x.mpg.NombresFacilitador ?? string.Empty,
                    ApellidosFacilitador = x.mpg.ApellidosFacilitador ?? string.Empty,
                    OldCodigoFacilitador = string.IsNullOrEmpty(x.te.Propietario3)
                        ? string.Empty
                        : (x.te.Propietario3.Contains("@") 
                            ? x.te.Propietario3.Substring(0, x.te.Propietario3.IndexOf("@")) 
                            : x.te.Propietario3)
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            return result;
        }
    }
}

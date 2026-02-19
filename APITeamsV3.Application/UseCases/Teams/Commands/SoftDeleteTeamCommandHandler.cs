using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SoftDeleteTeamCommandHandler : IRequestHandler<SoftDeleteTeamCommand, Unit>
    {
        private readonly ISmartDbContext _context;

        public SoftDeleteTeamCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(SoftDeleteTeamCommand request, CancellationToken cancellationToken)
        {
            // Option 29 Logic: ELIMINA LOS EQUIPOS (Soft Delete)
            var sql = @"
                UPDATE TeamsEquipos
                SET EstadoTeam = 'I',
                  UsuarioModificacion = 1,
                  FechaModificacion = GETDATE()
                WHERE IdTeamsGroup = {0};

                UPDATE TeamsUsuarios
                SET Estado = 'I',
                  UsuarioModificacion = 1,
                  FechaModificacion = GETDATE()
                WHERE IdTeams = {0};

                UPDATE TeamsHorarios
                SET Estado = 'I',
                  UsuarioModificacion = 1,
                  FechaModificacion = GETDATE()
                WHERE IdTeams = {0};";

            await _context.Database.ExecuteSqlRawAsync(sql, request.IdTeamsGroup);

            return Unit.Value;
        }
    }
}

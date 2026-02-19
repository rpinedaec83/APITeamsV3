using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class CreateTeamRecordCommandHandler : IRequestHandler<CreateTeamRecordCommand, Unit>
    {
        private readonly ISmartDbContext _context;

        public CreateTeamRecordCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(CreateTeamRecordCommand request, CancellationToken cancellationToken)
        {
            // Option 31 Logic: CREA LOS NUEVOS GRUPOS
            var sql = @"
                INSERT INTO TeamsEquipos (
                    IdTeamsGroup,
                    Propietario1,
                    Propietario2,
                    Propietario3,
                    Propietario4,
                    NombreTeam,
                    DescripcionTeam,
                    MailNickName,
                    EstadoTeam,
                    IdSeccionSmart,
                    IsActive,
                    UsuarioCreacion,
                    FechaCreacion
                  )
                VALUES (
                    {0},
                    {1},
                    {2},
                    {3},
                    {4},
                    {5},
                    {6},
                    {7},
                    'A',
                    {8},
                    'I',
                    1,
                    GETDATE()
                  );";

            await _context.Database.ExecuteSqlRawAsync(sql, 
                request.IdTeamsGroup, 
                request.Propietario1, 
                request.Propietario2, 
                request.Propietario3, 
                request.Propietario4, 
                request.NombreTeam, 
                request.DescripcionTeam, 
                request.MailNickName, 
                request.IdSeccionSmart);

            return Unit.Value;
        }
    }
}

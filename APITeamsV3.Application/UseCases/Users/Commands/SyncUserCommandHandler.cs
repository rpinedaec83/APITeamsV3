using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Users.Commands
{
    public class SyncUserCommandHandler : IRequestHandler<SyncUserCommand, Unit>
    {
        private readonly ISmartDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public SyncUserCommandHandler(ISmartDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(SyncUserCommand request, CancellationToken cancellationToken)
        {
            // Option 20 Logic: UPSERT USER
            // Logic:
            // IF EXISTS (SELECT 1 FROM TeamsUsuarios WHERE idTeams = @idTeamsGroup AND CodigoAlumno = @CodigoAlumno)
            //   UPDATE ...
            // ELSE
            //   INSERT ...

            var sql = @"
                IF EXISTS (
                    SELECT 1
                    FROM TeamsUsuarios WITH (NOLOCK)
                    WHERE idTeams = {0}
                      AND CodigoAlumno = {1}
                  )
                BEGIN
                    UPDATE TeamsUsuarios
                    SET Nombres = {2},
                        Apellidos = {3},
                        Email = {4},
                        Estado = 'A',
                        UsuarioModificacion = {6},
                        FechaModificacion = GETDATE()
                    WHERE CodigoAlumno = {1}
                      AND idTeams = {0}
                END
                ELSE
                BEGIN
                    INSERT INTO TeamsUsuarios (
                        idTeams,
                        CodigoAlumno,
                        Nombres,
                        Apellidos,
                        Email,
                        Tipo,
                        Estado,
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
                        'A',
                        {6},
                        GETDATE()
                      )
                END";

            await _context.Database.ExecuteSqlRawAsync(sql, 
                request.IdTeamsGroup, 
                request.CodigoAlumno, 
                request.Nombres, 
                request.Apellidos, 
                request.Email, 
                request.Tipo,
                _currentUserService.UserIdInt ?? 99);

            return Unit.Value;
        }
    }
}


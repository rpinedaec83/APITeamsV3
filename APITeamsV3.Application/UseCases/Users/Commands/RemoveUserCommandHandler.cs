using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Users.Commands
{
    public class RemoveUserCommandHandler : IRequestHandler<RemoveUserCommand, Unit>
    {
        private readonly ISmartDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public RemoveUserCommandHandler(ISmartDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(RemoveUserCommand request, CancellationToken cancellationToken)
        {
            // Option 21 Logic: ELIMINA MIEMBROS DEL EQUIPOS (Soft Delete)
            var sql = @"
                UPDATE TeamsUsuarios
                SET Estado = 'I',
                  UsuarioModificacion = {2},
                  FechaModificacion = GETDATE()
                WHERE CodigoAlumno = {0}
                  AND idTeams = {1};";

            await _context.Database.ExecuteSqlRawAsync(sql, request.CodigoAlumno, request.IdTeamsGroup, _currentUserService.UserIdInt ?? 99);

            return Unit.Value;
        }
    }
}


using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class UpdateSectionJoinUrlCommandHandler : IRequestHandler<UpdateSectionJoinUrlCommand, bool>
    {
        private readonly ISmartDbContext _context;

        public UpdateSectionJoinUrlCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdateSectionJoinUrlCommand request, CancellationToken cancellationToken)
        {
            // Option 11 Legacy Logic:
            // UPDATE SeccionHorario
            // set UrlClaseVirtual = @JoinUrl,
            //   IdEvento = @IdEvento
            // where IdSeccion = @IdSeccion

            var sql = @"
                UPDATE SeccionHorario
                SET UrlClaseVirtual = {0},
                    IdEvento = {1}
                WHERE IdSeccion = {2};
            ";

            await _context.Database.ExecuteSqlRawAsync(sql, request.JoinUrl, request.IdEvento, request.IdSeccion);

            return true;
        }
    }
}

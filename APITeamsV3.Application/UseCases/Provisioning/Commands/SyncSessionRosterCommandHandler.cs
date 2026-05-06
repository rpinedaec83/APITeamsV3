using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionRosterCommandHandler : IRequestHandler<SyncSessionRosterCommand>
    {
        private readonly ISmartDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public SyncSessionRosterCommandHandler(ISmartDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task Handle(SyncSessionRosterCommand request, CancellationToken cancellationToken)
        {
            if (request.Mode == SessionRosterSyncType.FullSync)
            {
                // Logic to manage individual students in TeamsHorarios was removed
                // as per new requirement: 'en teamshorario solo debe grabar los bloques no por cada alumno/docente'.
                await Task.CompletedTask;

                // Logic to manage individual students in TeamsHorarios was removed
                // as per new requirement: 'en teamshorario solo debe grabar los bloques no por cada alumno/docente'.
                await Task.CompletedTask;
            }
            else if (request.Mode == SessionRosterSyncType.EventSync)
            {
                // Logic to manage individual students in TeamsHorarios was removed
                // as per new requirement: 'en teamshorario solo debe grabar los bloques no por cada alumno/docente'.
                await Task.CompletedTask;
            }

            return;
        }
    }
}


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
        private readonly ITeamsAgendaService _agendaService;

        public SyncSessionRosterCommandHandler(
            ISmartDbContext context,
            ICurrentUserService currentUserService,
            ITeamsAgendaService agendaService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _agendaService = agendaService;
        }

        public async Task Handle(SyncSessionRosterCommand request, CancellationToken cancellationToken)
        {
            if (request.IdSeccion > 0)
            {
                await _agendaService.EnsureTeacherCoorganizerForSectionAsync(request.IdSeccion, cancellationToken);
            }
        }
    }
}


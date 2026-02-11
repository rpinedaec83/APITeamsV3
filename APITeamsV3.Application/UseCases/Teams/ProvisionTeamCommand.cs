using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Application.UseCases.Teams
{
    public record ProvisionTeamCommand(int IdSeccion, string OwnerEmail) : IRequest<int>;

    public class ProvisionTeamCommandHandler : IRequestHandler<ProvisionTeamCommand, int>
    {
        private readonly IBackgroundJobService _jobService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISmartDbContext _context;

        public ProvisionTeamCommandHandler(
            IBackgroundJobService jobService, 
            ITenantProvider tenantProvider,
            ISmartDbContext context)
        {
            _jobService = jobService;
            _tenantProvider = tenantProvider;
            _context = context;
        }

        public async Task<int> Handle(ProvisionTeamCommand request, CancellationToken cancellationToken)
        {
            // 1. Get Section (Read-Only View) from SmartDB to verify it exists
            var seccion = await _context.Set<Seccion>()
                .FirstOrDefaultAsync(s => s.IdSeccion == request.IdSeccion, cancellationToken);

            if (seccion == null)
            {
                throw new System.Exception($"Section {request.IdSeccion} not found.");
            }

            // 2. Enqueue Job
            var tenant = _tenantProvider.GetCurrentTenant();
            return await _jobService.EnqueueJobAsync("ProvisionTeam", request.IdSeccion.ToString(), tenant.CompanyKey);
        }
    }
}

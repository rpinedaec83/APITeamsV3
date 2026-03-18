using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Application.UseCases.Teams.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncAllTeamsCommandHandler : IRequestHandler<SyncAllTeamsCommand, SyncAllTeamsResult>
    {
        private readonly IMediator _mediator;
        private readonly IHangfireJobService _jobService;

        public SyncAllTeamsCommandHandler(IMediator mediator, IHangfireJobService jobService)
        {
            _mediator = mediator;
            _jobService = jobService;
        }

        public async Task<SyncAllTeamsResult> Handle(SyncAllTeamsCommand request, CancellationToken cancellationToken)
        {
            // Step 1: Get all section IDs using Option 19 logic
            var sectionIds = await _mediator.Send(new GetAllSectionsToSyncQuery(request.Sede), cancellationToken);

            var jobIds = new List<string>();

            // Step 2: Enqueue a full sync chain for each section
            foreach (var idSeccion in sectionIds)
            {
                var jobId = _jobService.EnqueueFullSectionSync(idSeccion);
                jobIds.Add(jobId);
            }

            return new SyncAllTeamsResult
            {
                TotalSections = sectionIds.Count,
                SectionIds = sectionIds,
                JobIds = jobIds
            };
        }
    }
}

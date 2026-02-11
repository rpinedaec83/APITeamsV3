using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Jobs
{
    public record GetJobQuery(int Id) : IRequest<SyncJob?>;

    public class GetJobQueryHandler : IRequestHandler<GetJobQuery, SyncJob?>
    {
        private readonly IBackgroundJobService _jobService;

        public GetJobQueryHandler(IBackgroundJobService jobService)
        {
            _jobService = jobService;
        }

        public async Task<SyncJob?> Handle(GetJobQuery request, CancellationToken cancellationToken)
        {
            return await _jobService.GetJobAsync(request.Id);
        }
    }
}

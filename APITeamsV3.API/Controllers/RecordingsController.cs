using APITeamsV3.Application.Common.Models;
using APITeamsV3.Application.UseCases.Recordings.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/recordings")]
    [Authorize(Roles = "ADMIN,IT,GESTION")]
    public class RecordingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RecordingsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("transfer")]
        public async Task<ActionResult<RecordingTransferResult>> Transfer(
            [FromBody] TransferRecordingsRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TeamGroupId))
            {
                return BadRequest(new { message = "teamGroupId es requerido." });
            }

            var command = new TransferRecordingsCommand
            {
                JobId = request.JobId,
                OrganizerUserPrincipalName = request.OrganizerUserPrincipalName,
                OrganizerUserId = request.OrganizerUserId,
                TeamGroupId = request.TeamGroupId,
                ChannelName = request.ChannelName,
                DestinationFolderPath = request.DestinationFolderPath,
                SourceFolderPath = request.SourceFolderPath,
                CourseName = request.CourseName,
                Section = request.Section,
                SectionId = request.SectionId,
                SectionCode = request.SectionCode,
                StartDateUtc = request.StartDateUtc,
                EndDateUtc = request.EndDateUtc,
                LookBackHours = request.LookBackHours,
                MaxFiles = request.MaxFiles
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }

    public class TransferRecordingsRequest
    {
        public string? JobId { get; set; }
        public string? OrganizerUserPrincipalName { get; set; }
        public string? OrganizerUserId { get; set; }
        public string TeamGroupId { get; set; } = string.Empty;
        public string? ChannelName { get; set; }
        public string? DestinationFolderPath { get; set; }
        public string? SourceFolderPath { get; set; }
        public string? CourseName { get; set; }
        public string? Section { get; set; }
        public int? SectionId { get; set; }
        public string? SectionCode { get; set; }
        public System.DateTimeOffset? StartDateUtc { get; set; }
        public System.DateTimeOffset? EndDateUtc { get; set; }
        public int? LookBackHours { get; set; }
        public int? MaxFiles { get; set; }
    }
}

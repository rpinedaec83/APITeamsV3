using APITeamsV3.Application.Common.Models;
using MediatR;

namespace APITeamsV3.Application.UseCases.Recordings.Commands
{
    public class TransferRecordingsCommand : RecordingTransferRequest, IRequest<RecordingTransferResult>
    {
    }
}

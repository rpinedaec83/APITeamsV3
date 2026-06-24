using APITeamsV3.Application.Common.Models;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ITeamsRecordingTransferService
    {
        Task<RecordingTransferResult> TransferAsync(RecordingTransferRequest request, CancellationToken cancellationToken = default);
        Task<DriveQuotaResult> GetStorageQuotaAsync(CancellationToken cancellationToken = default);
    }
}

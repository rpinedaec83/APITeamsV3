using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionFacilitatorCommand : IRequest
    {
        public int IdSeccion { get; }
        public string? JobId { get; }

        public SyncSessionFacilitatorCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }
}

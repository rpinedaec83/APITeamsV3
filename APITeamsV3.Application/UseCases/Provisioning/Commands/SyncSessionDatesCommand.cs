using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionDatesCommand : IRequest
    {
        public int IdSeccion { get; }
        public string? JobId { get; }

        public SyncSessionDatesCommand(int idSeccion, string? jobId = null)
        {
            IdSeccion = idSeccion;
            JobId = jobId;
        }
    }
}

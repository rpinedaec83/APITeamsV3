using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionDatesCommand : IRequest<bool>
    {
        public int IdSeccion { get; set; }

        public SyncSessionDatesCommand(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

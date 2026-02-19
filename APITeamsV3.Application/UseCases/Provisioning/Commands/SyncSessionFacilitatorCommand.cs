using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionFacilitatorCommand : IRequest<bool>
    {
        public int IdSeccion { get; set; }

        public SyncSessionFacilitatorCommand(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

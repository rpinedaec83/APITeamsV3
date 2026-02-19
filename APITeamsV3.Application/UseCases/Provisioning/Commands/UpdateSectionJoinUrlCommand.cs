using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class UpdateSectionJoinUrlCommand : IRequest<bool>
    {
        public int IdSeccion { get; set; }
        public string JoinUrl { get; set; }
        public string IdEvento { get; set; }

        public UpdateSectionJoinUrlCommand(int idSeccion, string joinUrl, string idEvento)
        {
            IdSeccion = idSeccion;
            JoinUrl = joinUrl;
            IdEvento = idEvento;
        }
    }
}

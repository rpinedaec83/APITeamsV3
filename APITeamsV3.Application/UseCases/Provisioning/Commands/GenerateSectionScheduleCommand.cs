using MediatR;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class GenerateSectionScheduleCommand : IRequest<bool>
    {
        public int IdSeccion { get; set; }
        public bool Force { get; set; }

        public GenerateSectionScheduleCommand(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

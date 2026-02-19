using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class GetNextSessionToProvisionQuery : IRequest<NextSessionDto?>
    {
        public int IdSeccion { get; set; }

        public GetNextSessionToProvisionQuery(int idSeccion)
        {
            IdSeccion = idSeccion;
        }
    }
}

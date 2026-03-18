using MediatR;
using System.Collections.Generic;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public record GetAllSectionsToSyncQuery(string Sede) : IRequest<List<int>>;
}

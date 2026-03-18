using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public record SyncAllTeamsCommand(string Sede) : IRequest<SyncAllTeamsResult>;
}

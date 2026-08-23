using MediatR;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public record SyncObsoleteTeamsCommand(int? IdSeccion = null) : IRequest<SyncObsoleteTeamsResult>;
}

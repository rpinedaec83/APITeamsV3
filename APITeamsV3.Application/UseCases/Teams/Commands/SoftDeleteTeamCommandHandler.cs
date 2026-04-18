using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SoftDeleteTeamCommandHandler : IRequestHandler<SoftDeleteTeamCommand, Unit>
    {
        private readonly ISmartDbContext _context;
        private readonly ITeamsLogOperativoRepository _logRepository;

        public SoftDeleteTeamCommandHandler(ISmartDbContext context, ITeamsLogOperativoRepository logRepository)
        {
            _context = context;
            _logRepository = logRepository;
        }

        public async Task<Unit> Handle(SoftDeleteTeamCommand request, CancellationToken cancellationToken)
        {
            // 1. Soft-delete TeamsEquipos
            var team = await _context.Set<TeamEntity>()
                .FirstOrDefaultAsync(t => t.IdTeamsGroup == request.IdTeamsGroup, cancellationToken);

            if (team != null)
            {
                team.EstadoTeam = "I";
                team.IsActive = "I";
                team.FechaModificacion = DateTime.UtcNow;

                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Info",
                    EntidadAfectada = "Team",
                    Referencia = request.IdTeamsGroup,
                    Mensaje = $"[Manual/Command] Team {request.IdTeamsGroup} SOFT-DELETED (Status I). Affected: Team, Members, Sessions.",
                    Fecha = DateTime.UtcNow,
                    Severidad = "Low"
                });
            }

            // 2. Soft-delete TeamsUsuarios
            var members = await _context.Set<TeamMember>()
                .Where(m => m.IdTeams == request.IdTeamsGroup && m.Estado == "A")
                .ToListAsync(cancellationToken);

            foreach (var member in members)
            {
                member.Estado = "I";
                member.FechaModificacion = DateTime.UtcNow;
            }

            // 3. Soft-delete TeamsHorarios
            var sessions = await _context.Set<TeamSession>()
                .Where(s => s.IdTeams == request.IdTeamsGroup && s.Estado == "A")
                .ToListAsync(cancellationToken);

            foreach (var session in sessions)
            {
                session.Estado = "I";
                session.FechaModificacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

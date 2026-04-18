using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class CreateTeamRecordCommandHandler : IRequestHandler<CreateTeamRecordCommand, Unit>
    {
        private readonly ISmartDbContext _context;
        private readonly ITeamsLogOperativoRepository _logRepository;

        public CreateTeamRecordCommandHandler(ISmartDbContext context, ITeamsLogOperativoRepository logRepository)
        {
            _context = context;
            _logRepository = logRepository;
        }

        public async Task<Unit> Handle(CreateTeamRecordCommand request, CancellationToken cancellationToken)
        {
            // Lógica para persistir el nuevo registro de equipo
            var newTeam = new TeamEntity
            {
                IdTeamsGroup = request.IdTeamsGroup,
                Propietario1 = request.Propietario1 ?? string.Empty,
                Propietario2 = request.Propietario2 ?? string.Empty,
                Propietario3 = request.Propietario3,
                Propietario4 = request.Propietario4,
                NombreTeam = request.NombreTeam ?? string.Empty,
                DescripcionTeam = request.DescripcionTeam ?? string.Empty,
                MailNickName = request.MailNickName ?? string.Empty,
                EstadoTeam = "A", // Forzamos a Activo al crear
                IdSeccionSmart = request.IdSeccionSmart,
                IsActive = "A",
                FechaCreacion = DateTime.Now
            };

            _context.TeamsEquipos.Add(newTeam);
            await _context.SaveChangesAsync(cancellationToken);

            await _logRepository.LogAsync(new TeamsLogOperativo
            {
                Tipo = "Info",
                EntidadAfectada = "Team",
                Referencia = request.IdTeamsGroup,
                Mensaje = $"[Manual/Command] Team {request.IdTeamsGroup} registered in DB for Section {request.IdSeccionSmart}",
                Fecha = DateTime.UtcNow,
                Severidad = "Low"
            });

            return Unit.Value;
        }
    }
}

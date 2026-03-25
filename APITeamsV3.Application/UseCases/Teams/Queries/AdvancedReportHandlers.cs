using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Queries
{
    public class AdvancedReportHandlers : 
        IRequestHandler<GetScheduleReportQuery, List<ScheduleReportDto>>,
        IRequestHandler<GetTeamMembersReportQuery, List<TeamMemberReportDto>>,
        IRequestHandler<GetSmartVsTeamsReportQuery, List<SmartVsTeamsReportDto>>,
        IRequestHandler<GetSyncProgressReportQuery, List<SyncProgressReportDto>>
    {
        private readonly ISmartDbContext _context;

        public AdvancedReportHandlers(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<List<ScheduleReportDto>> Handle(GetScheduleReportQuery request, CancellationToken cancellationToken)
        {
            // Join Seccion with SeccionHorario
            var query = from s in _context.Set<Seccion>().AsNoTracking()
                        join h in _context.Set<SeccionHorario>().AsNoTracking() on s.IdSeccion equals h.IdSeccion
                        select new ScheduleReportDto
                        {
                            IdSeccion = s.IdSeccion,
                            Seccion = s.GrupoCodigo,
                            Curso = s.CursoNombre,
                            Dia = h.Dia,
                            Inicio = h.Inicio,
                            Fin = h.Fin,
                            Sede = s.SedeNombre,
                            Facilitador = s.NombresFacilitador
                        };

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<List<TeamMemberReportDto>> Handle(GetTeamMembersReportQuery request, CancellationToken cancellationToken)
        {
            var query = from tm in _context.Set<TeamMember>().AsNoTracking()
                        join t in _context.Set<TeamEntity>().AsNoTracking() on tm.IdTeams equals t.IdTeamsGroup
                        select new TeamMemberReportDto
                        {
                            IdTeamsGroup = t.IdTeamsGroup,
                            NombreTeam = t.NombreTeam,
                            CodigoAlumno = tm.CodigoAlumno,
                            NombreAlumno = tm.Nombres + " " + tm.Apellidos,
                            Correo = tm.Email,
                            Rol = tm.Tipo == "F" ? "Owner" : "Member", // Adjusted logic for F=Facilitador
                            Estado = tm.Estado
                        };

            if (!string.IsNullOrEmpty(request.IdTeamsGroup))
            {
                query = query.Where(x => x.IdTeamsGroup == request.IdTeamsGroup);
            }

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<List<SmartVsTeamsReportDto>> Handle(GetSmartVsTeamsReportQuery request, CancellationToken cancellationToken)
        {
            // Compare AlumnoCurso (Smart) vs TeamsUsuarios (Teams)
            // This is a complex query, let's simplify for the report
            var sections = await _context.Set<Seccion>().AsNoTracking().ToListAsync(cancellationToken);
            var result = new List<SmartVsTeamsReportDto>();

            foreach (var s in sections)
            {
                int smartCount = await _context.Set<AlumnoCurso>().CountAsync(ac => ac.IdSeccion == s.IdSeccion, cancellationToken);
                
                var team = await _context.Set<TeamEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.IdSeccionSmart == s.IdSeccion, cancellationToken);
                int teamsCount = 0;
                if (team != null)
                {
                    teamsCount = await _context.Set<TeamMember>().CountAsync(tm => tm.IdTeams == team.IdTeamsGroup, cancellationToken);
                }

                result.Add(new SmartVsTeamsReportDto
                {
                    IdSeccion = s.IdSeccion,
                    Seccion = s.GrupoCodigo,
                    AlumnosSmart = smartCount,
                    AlumnosTeams = teamsCount,
                    Diferencia = smartCount - teamsCount,
                    EstadoTeam = team?.EstadoTeam ?? "N/A"
                });
            }

            return result;
        }

        public async Task<List<SyncProgressReportDto>> Handle(GetSyncProgressReportQuery request, CancellationToken cancellationToken)
        {
            var sedes = await _context.Set<Seccion>().AsNoTracking().Select(s => s.SedeNombre).Distinct().ToListAsync(cancellationToken);
            var result = new List<SyncProgressReportDto>();

            foreach (var sede in sedes)
            {
                var total = await _context.Set<Seccion>().CountAsync(s => s.SedeNombre == sede, cancellationToken);
                var synced = await (from s in _context.Set<Seccion>().AsNoTracking()
                                    join t in _context.Set<TeamEntity>().AsNoTracking() on s.IdSeccion equals t.IdSeccionSmart
                                    where s.SedeNombre == sede && t.EstadoTeam == "A"
                                    select s).CountAsync(cancellationToken);

                var errors = await _context.Set<TeamsLogOperativo>()
                    .CountAsync(l => l.Tipo == "Error" && l.Fecha >= DateTime.UtcNow.AddDays(-1), cancellationToken);

                result.Add(new SyncProgressReportDto
                {
                    Sede = sede,
                    TotalSecciones = total,
                    SeccionesSincronizadas = synced,
                    PorcentajeAvance = total > 0 ? (decimal)synced / total * 100 : 0,
                    ErroresUltimas24h = errors
                });
            }

            return result;
        }
    }
}

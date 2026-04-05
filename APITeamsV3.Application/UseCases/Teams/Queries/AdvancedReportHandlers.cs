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
        private readonly ICentralDbContext _centralContext;
        private readonly ITenantProvider _tenantProvider;

        public AdvancedReportHandlers(
            ISmartDbContext context,
            ICentralDbContext centralContext,
            ITenantProvider tenantProvider)
        {
            _context = context;
            _centralContext = centralContext;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<ScheduleReportDto>> Handle(GetScheduleReportQuery request, CancellationToken cancellationToken)
        {
            var activeSedes = await GetActiveSedeNamesAsync(cancellationToken);

            // Join Seccion with HorarioSesion
            var query = from s in _context.Set<Seccion>().AsNoTracking()
                        join h in _context.Set<HorarioSesion>().AsNoTracking() on s.IdSeccion equals h.IdSeccion
                        where activeSedes.Count == 0 || activeSedes.Contains(s.SedeNombre)
                        select new ScheduleReportDto
                        {
                            IdSeccion = s.IdSeccion,
                            Seccion = s.GrupoCodigo,
                            Curso = s.CursoNombre,
                            Dia = h.Fecha.DayOfWeek.ToString(),
                            Inicio = h.Inicio,
                            Fin = h.Fin,
                            Sede = s.SedeNombre,
                            Facilitador = s.NombresFacilitador
                        };

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<List<TeamMemberReportDto>> Handle(GetTeamMembersReportQuery request, CancellationToken cancellationToken)
        {
            var activeSedes = await GetActiveSedeNamesAsync(cancellationToken);

            var query = from tm in _context.Set<TeamMember>().AsNoTracking()
                        join t in _context.Set<TeamEntity>().AsNoTracking() on tm.IdTeams equals t.IdTeamsGroup
                        join s in _context.Set<Seccion>().AsNoTracking() on t.IdSeccionSmart equals s.IdSeccion
                        where activeSedes.Count == 0 || activeSedes.Contains(s.SedeNombre)
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
            var activeSedes = await GetActiveSedeNamesAsync(cancellationToken);

            // Compare AlumnoCurso (Smart) vs TeamsUsuarios (Teams)
            // This is a complex query, let's simplify for the report
            var sections = await _context.Set<Seccion>()
                .AsNoTracking()
                .Where(s => activeSedes.Count == 0 || activeSedes.Contains(s.SedeNombre))
                .ToListAsync(cancellationToken);
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
            var activeSedes = await GetActiveSedeNamesAsync(cancellationToken);
            var sedes = await _context.Set<Seccion>()
                .AsNoTracking()
                .Where(s => activeSedes.Count == 0 || activeSedes.Contains(s.SedeNombre))
                .Select(s => s.SedeNombre)
                .Distinct()
                .ToListAsync(cancellationToken);
            var result = new List<SyncProgressReportDto>();

            foreach (var sede in sedes)
            {
                var total = await _context.Set<Seccion>().CountAsync(s => s.SedeNombre == sede, cancellationToken);
                var synced = await (from s in _context.Set<Seccion>().AsNoTracking()
                                    join t in _context.Set<TeamEntity>().AsNoTracking() on s.IdSeccion equals t.IdSeccionSmart
                                    where s.SedeNombre == sede && t.EstadoTeam == "A"
                                    select s).CountAsync(cancellationToken);

                var errors = await _context.Set<APITeamsV3.Domain.Entities.TeamsLogOperativo>()
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

        private async Task<HashSet<string>> GetActiveSedeNamesAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            if (string.IsNullOrWhiteSpace(tenant.CompanyKey))
            {
                return [];
            }

            var normalizedCompanyKey = tenant.CompanyKey.Trim().ToLowerInvariant();
            var company = await _centralContext.CompanyConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && c.CompanyKey.ToLower() == normalizedCompanyKey,
                    cancellationToken);

            if (company == null)
            {
                return [];
            }

            var sedes = await _centralContext.CompanySedes
                .AsNoTracking()
                .Where(s => s.CompanyConfigId == company.Id && s.IsActive)
                .Select(s => s.Nombre)
                .ToListAsync(cancellationToken);

            return sedes
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}

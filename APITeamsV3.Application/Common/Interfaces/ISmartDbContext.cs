using Microsoft.EntityFrameworkCore;
using APITeamsV3.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ISmartDbContext
    {
        DatabaseFacade Database { get; }
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        DbSet<SeccionTable> SeccionTable { get; }
        DbSet<EmpresaSedeParametro> EmpresaSedeParametro { get; }
        DbSet<TeamsProgramacionGeneral> TeamsProgramacionGeneral { get; }
        DbSet<TeamsProgramacionAlumnos> TeamsProgramacionAlumnos { get; }
        DbSet<TeamEntity> TeamsEquipos { get; }
        DbSet<TeamMember> TeamsUsuarios { get; }
        DbSet<APITeamsV3.Domain.Entities.TeamSession> TeamsHorarios { get; set; }
        DbSet<APITeamsV3.Domain.Entities.AplicativoTeams> AplicativosTeams { get; set; }
        DbSet<ReunionAsistencia> TeamsReunionAsistencia { get; }
        DbSet<ReunionAsistenciaDetalle> TeamsReunionAsistenciaDetalle { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}

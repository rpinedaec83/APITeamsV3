using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.Infrastructure.Persistence.Contexts
{
    public class SmartDbContext : DbContext, ISmartDbContext
    {
        public DbSet<APITeamsV3.Domain.Entities.Parametro> Parametro { get; set; }
        private readonly ITenantProvider _tenantProvider;

        public SmartDbContext(DbContextOptions<SmartDbContext> options, ITenantProvider tenantProvider) 
            : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // If options were already configured (e.g., in tests), don't override
            if (!optionsBuilder.IsConfigured)
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                if (!string.IsNullOrEmpty(tenant.ConnectionString))
                {
                    optionsBuilder.UseSqlServer(tenant.ConnectionString);
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Parametro
            modelBuilder.Entity<APITeamsV3.Domain.Entities.Parametro>(entity =>
            {
                entity.ToTable("Parametro");
                entity.HasKey(e => e.Nombre);
            });

            // TeamsEquipos
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamEntity>(entity =>
            {
                entity.ToTable("TeamsEquipos");
                entity.HasKey(e => e.IdTeamsGroup);
                entity.Property(e => e.EstadoTeam).HasMaxLength(1);
                entity.Property(e => e.IsActive).HasMaxLength(1);
                
                // Explicit FK Configuration
                entity.HasOne(e => e.Seccion)
                      .WithMany() // Seccion has no collection of Teams
                      .HasForeignKey(e => e.IdSeccionSmart);
            });

            // TeamsUsuarios
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamMember>(entity =>
            {
                entity.ToTable("TeamsUsuarios");
                entity.HasKey(e => e.IdUsuario);
                entity.Property(e => e.Estado).HasMaxLength(1);
                entity.Property(e => e.Tipo).HasMaxLength(1);
            });

            // TeamsHorarios
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamSession>(entity =>
            {
                entity.ToTable("TeamsHorarios");
                // Assuming composite key based on usage, or Id if exists. 
                // For safety regarding legacy schema without full knowledge, we might rely on EF shadow properties if PK is missing, 
                // but usually legacy has some PK. Let's assume Id or composite.
                // The MERGE statement in SQL used (idTeams, idEvento), let's assume that's unique enough for business logic
                // but for EF we need a real PK. Let's assume there is an ID column or use composite.
                entity.HasKey(e => new { e.IdTeams, e.IdEvento }); 
            });

            // Read-Only Views
            modelBuilder.Entity<APITeamsV3.Domain.Entities.Seccion>(entity =>
            {
                entity.ToView("vw_MatriculasActivas"); // Conceptual view name
                entity.HasKey(e => e.IdSeccion);
            });

            // Legacy Tables & Views
            modelBuilder.Entity<APITeamsV3.Domain.Entities.Alumno>(entity =>
            {
                entity.ToView("vw_AlumnoMaster"); // Map to View
                entity.HasKey(e => e.IdAlumno);
                entity.Property(e => e.Codigo).HasColumnName("Codigo"); // View Column
                entity.Property(e => e.Nombre).HasColumnName("Nombre"); // View Column
            });

            modelBuilder.Entity<APITeamsV3.Domain.Entities.AlumnoCurso>(entity =>
            {
                entity.ToTable("AlumnoCurso");
                entity.HasKey(e => e.IdAlumnoCurso);
                entity.HasOne(e => e.Alumno).WithMany().HasForeignKey(e => e.IdAlumno);
                entity.HasOne(e => e.Seccion).WithMany().HasForeignKey(e => e.IdSeccion);
            });
        }
    }
}

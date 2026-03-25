using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;

namespace APITeamsV3.Infrastructure.Persistence.Contexts
{
    public class SmartDbContext : DbContext, ISmartDbContext
    {
        public DbSet<APITeamsV3.Domain.Entities.Parametro> Parametro { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamsLogOperativo> TeamsLogOperativo { get; set; }
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
                    optionsBuilder.UseSqlServer(tenant.ConnectionString, sqlOptions => 
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
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

            // TeamsLogOperativo
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamsLogOperativo>(entity =>
            {
                entity.ToTable("TeamsLogOperativo");
                entity.HasKey(e => e.Id);
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

            // TeamsProgramacionGeneral
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamsProgramacionGeneral>(entity =>
            {
                entity.ToTable("TeamsProgramacionGeneral");
                entity.HasKey(e => new { e.IdCurso, e.IdPeriodo }); // Assuming Composite Key based on usage, or add Id if exists in DB
                // If the table has no PK, use HasNoKey() but EF requires a key for tracking. 
                // Let's assume IdCurso (IdSeccion) is unique per Periodo? No, IdCurso is IdSeccion. 
                // Actually, IdCurso + something? 
                // Looking at legacy SQL: DELETE WHERE IdCurso = @IdSeccion.
                // It seems to be a staging table. It might not have a PK.
                // Let's check if we can define a composite key that makes sense, or use Keyless entity.
                // For now, let's try HasKey(e => e.IdCurso) but it might not be unique globally? 
                // Ah, it's "Programacion General", one row per section?
                // SQL: INSERT INTO ... SELECT DISTINCT ... FROM Seccion ... WHERE IdSeccion = @IdSeccion
                // Yes, it seems one row per section.
                entity.HasKey(e => e.IdCurso);
            });

            // TeamsProgramacionAlumnos
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamsProgramacionAlumnos>(entity =>
            {
                entity.ToTable("TeamsProgramacionAlumnos");
                // This definitely has multiple rows per course.
                // It tracks students.
                entity.HasKey(e => new { e.IdCurso, e.CodigoAlumno });
            });

            // SeccionHorario
            modelBuilder.Entity<APITeamsV3.Domain.Entities.SeccionHorario>(entity =>
            {
                entity.ToTable("SeccionHorario");
                entity.HasKey(e => e.IdSeccion);
            });

            // HorarioSesion
            modelBuilder.Entity<APITeamsV3.Domain.Entities.HorarioSesion>(entity =>
            {
                entity.ToTable("HorarioSesion");
                entity.HasKey(e => new { e.IdSeccion, e.IdHorario, e.Numero });
            });

            // Facilitador
            modelBuilder.Entity<APITeamsV3.Domain.Entities.Facilitador>(entity =>
            {
                entity.ToTable("Facilitador");
                entity.HasKey(e => e.IdFacilitador);
            });
        }
    }
}

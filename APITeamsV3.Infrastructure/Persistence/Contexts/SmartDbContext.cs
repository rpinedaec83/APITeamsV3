using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;

namespace APITeamsV3.Infrastructure.Persistence.Contexts
{
    public class SmartDbContext : DbContext, ISmartDbContext
    {
        public DbSet<APITeamsV3.Domain.Entities.Parametro> Parametro { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamsLogOperativo> TeamsLogOperativo { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.SeccionTable> SeccionTable { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.EmpresaSedeParametro> EmpresaSedeParametro { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamsProgramacionGeneral> TeamsProgramacionGeneral { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamsProgramacionAlumnos> TeamsProgramacionAlumnos { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamEntity> TeamsEquipos { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamMember> TeamsUsuarios { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamSession> TeamsHorarios { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.AplicativoTeams> AplicativosTeams { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.ReunionAsistencia> TeamsReunionAsistencia { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.ReunionAsistenciaDetalle> TeamsReunionAsistenciaDetalle { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.ReunionAsistenciaIntervalo> TeamsReunionAsistenciaIntervalo { get; set; }
        public DbSet<APITeamsV3.Domain.Entities.TeamsSeccionesPiloto> TeamsSeccionesPiloto { get; set; }
        private readonly ITenantProvider _tenantProvider;
        private readonly ICurrentUserService _currentUserService;

        public SmartDbContext(DbContextOptions<SmartDbContext> options, ITenantProvider tenantProvider, ICurrentUserService currentUserService) 
            : base(options)
        {
            _tenantProvider = tenantProvider;
            _currentUserService = currentUserService;
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
                entity.Property(e => e.Usuario).HasMaxLength(150);
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
                entity.HasKey(e => e.IdHorarioTeams);
                entity.Property(e => e.IdHorarioTeams).ValueGeneratedOnAdd();
            });

            // ReunionAsistencia
            modelBuilder.Entity<APITeamsV3.Domain.Entities.ReunionAsistencia>(entity =>
            {
                entity.ToTable("TeamsReunionAsistencia");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.MeetingReportId).IsUnique();
            });

            // ReunionAsistenciaDetalle
            modelBuilder.Entity<APITeamsV3.Domain.Entities.ReunionAsistenciaDetalle>(entity =>
            {
                entity.ToTable("TeamsReunionAsistenciaDetalle");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasOne(e => e.ReunionAsistencia)
                      .WithMany(a => a.Detalles)
                      .HasForeignKey(e => e.IdReunionAsistencia)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ReunionAsistenciaIntervalo
            modelBuilder.Entity<APITeamsV3.Domain.Entities.ReunionAsistenciaIntervalo>(entity =>
            {
                entity.ToTable("TeamsReunionAsistenciaIntervalo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasOne(e => e.ReunionAsistenciaDetalle)
                      .WithMany(a => a.Intervalos)
                      .HasForeignKey(e => e.IdReunionAsistenciaDetalle)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Read-Only Views
            modelBuilder.Entity<APITeamsV3.Domain.Entities.Seccion>(entity =>
            {
                entity.ToView("vw_MatriculasActivas"); // Conceptual view name
                entity.HasKey(e => e.IdSeccion);
            });

            // SeccionTable (Physical Table)
            modelBuilder.Entity<APITeamsV3.Domain.Entities.SeccionTable>(entity =>
            {
                entity.ToTable("Seccion");
                entity.HasKey(e => e.IdSeccion);
                entity.Ignore(e => e.IdPeriodo);
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

            // EmpresaSedeParametro
            modelBuilder.Entity<APITeamsV3.Domain.Entities.EmpresaSedeParametro>(entity =>
            {
                entity.ToTable("EmpresaSedeParametro");
                entity.HasKey(e => new { e.IdEmpresa, e.IdSede, e.IdParametro });
                entity.Property(e => e.Nombre).IsRequired();
            });

            // AplicativosTeams
            modelBuilder.Entity<APITeamsV3.Domain.Entities.AplicativoTeams>(entity =>
            {
                entity.ToTable("AplicativosTeams");
                entity.HasKey(e => e.IdAplicativo);
            });

            // TeamsSeccionesPiloto
            modelBuilder.Entity<APITeamsV3.Domain.Entities.TeamsSeccionesPiloto>(entity =>
            {
                entity.ToTable("TeamsSeccionesPiloto");
                entity.HasKey(e => e.IdSeccion);
                entity.Property(e => e.IdSeccion).ValueGeneratedNever();
            });

            // SmartSedeImport (Keyless for Raw SQL)
            modelBuilder.Entity<APITeamsV3.Domain.Entities.SmartSedeImport>(entity =>
            {
                entity.HasNoKey();
            });

            // SmartPilotSectionImport (Keyless for Raw SQL — pilot candidate sections query)
            modelBuilder.Entity<APITeamsV3.Domain.Entities.SmartPilotSectionImport>(entity =>
            {
                entity.HasNoKey();
            });

            // SmartPilotPeriodImport (Keyless for Raw SQL — pilot available periods query)
            modelBuilder.Entity<APITeamsV3.Domain.Entities.SmartPilotPeriodImport>(entity =>
            {
                entity.HasNoKey();
            });

            modelBuilder.Entity<TenancyStatsDto>(entity =>
            {
                entity.HasNoKey();
                entity.Property(e => e.PorEquiposActivos).HasPrecision(5, 2);
                entity.Property(e => e.PorDocente).HasPrecision(5, 2);
                entity.Property(e => e.PorCursoxAlumnos).HasPrecision(5, 2);
                entity.Property(e => e.PorAlumnos).HasPrecision(5, 2);
            });
        }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Default to 99 (System) if no user context, otherwise use actual user ID (defaulting to 888888 if authenticated but not mapped)
            int currentUserId = _currentUserService.UserIdInt ?? 99;

            foreach (var entry in ChangeTracker.Entries<APITeamsV3.Domain.Entities.TeamEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.UsuarioCreacion = currentUserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UsuarioModificacion = currentUserId;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

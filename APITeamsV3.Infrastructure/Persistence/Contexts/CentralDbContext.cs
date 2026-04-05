using Microsoft.EntityFrameworkCore;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.Graph.Models.Security;
using System.Reflection.Emit;

namespace APITeamsV3.Infrastructure.Persistence.Contexts
{
    public class CentralDbContext : DbContext, ICentralDbContext
    {
        public CentralDbContext(DbContextOptions<CentralDbContext> options) : base(options)
        {
        }

        public DbSet<CompanyConfig> CompanyConfigs { get; set; }
        public DbSet<CompanySede> CompanySedes { get; set; }
        public DbSet<SyncSchedule> SyncSchedules { get; set; }
        public DbSet<SyncScheduleExecution> SyncScheduleExecutions { get; set; }
        public DbSet<SyncJob> SyncJobs { get; set; }
        public DbSet<CompanyPilotSection> CompanyPilotSections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<CompanyConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CompanyKey).IsUnique();
                entity.HasIndex(e => e.FrontHost).IsUnique();
                entity.HasIndex(e => e.ApiHost).IsUnique();
            });

            modelBuilder.Entity<CompanySede>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CompanyConfigId, e.IdSede }).IsUnique();
            });

            modelBuilder.Entity<CompanyPilotSection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CompanyConfigId, e.IdSeccion }).IsUnique();
                
                entity.HasOne<CompanyConfig>()
                      .WithMany(c => c.PilotSections)
                      .HasForeignKey(e => e.CompanyConfigId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SyncSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<SyncScheduleExecution>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.TriggerSource).IsRequired();
                entity.Property(e => e.SedeCodes).IsRequired();
                entity.Property(e => e.JobIds).IsRequired();
                entity.Property(e => e.ErrorMessage).IsRequired();
                entity.HasIndex(e => e.SyncScheduleId);
                entity.HasIndex(e => e.CompanyConfigId);
                entity.HasIndex(e => e.TriggeredAtUtc);
            });

            modelBuilder.Entity<SyncJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.JobType).IsRequired();
                entity.Property(e => e.CompanyKey).IsRequired();
                entity.Property(e => e.TargetId).IsRequired();
                entity.Property(e => e.LastError).IsRequired(false);
                entity.Property(e => e.HangfireJobId).IsRequired(false);
                entity.HasIndex(e => e.Status);
            });
        }
    }
}

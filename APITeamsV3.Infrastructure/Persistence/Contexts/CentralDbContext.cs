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
        public DbSet<SyncJob> SyncJobs { get; set; }

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

            modelBuilder.Entity<SyncSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}

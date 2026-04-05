using System;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CentralDbContext))]
    [Migration("20260405130000_AddSyncScheduleExecutions")]
    public partial class AddSyncScheduleExecutions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncScheduleExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerSource = table.Column<string>(type: "TEXT", nullable: false),
                    SedeCodes = table.Column<string>(type: "TEXT", nullable: false),
                    TotalSections = table.Column<int>(type: "INTEGER", nullable: false),
                    EnqueuedJobsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    JobIds = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncScheduleExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncScheduleExecutions_CompanyConfigId",
                table: "SyncScheduleExecutions",
                column: "CompanyConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncScheduleExecutions_SyncScheduleId",
                table: "SyncScheduleExecutions",
                column: "SyncScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncScheduleExecutions_TriggeredAtUtc",
                table: "SyncScheduleExecutions",
                column: "TriggeredAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncScheduleExecutions");
        }
    }
}

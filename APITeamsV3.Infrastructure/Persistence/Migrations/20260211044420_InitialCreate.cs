using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyKey = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    FrontHost = table.Column<string>(type: "TEXT", nullable: false),
                    ApiHost = table.Column<string>(type: "TEXT", nullable: false),
                    SpaClientId = table.Column<string>(type: "TEXT", nullable: true),
                    SmartConnectionString = table.Column<string>(type: "TEXT", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GraphTenantId = table.Column<string>(type: "TEXT", nullable: false),
                    GraphClientId = table.Column<string>(type: "TEXT", nullable: false),
                    GraphClientSecretRef = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultChannelName = table.Column<string>(type: "TEXT", nullable: false),
                    MeetingPolicyMode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyKey = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyConfigs_ApiHost",
                table: "CompanyConfigs",
                column: "ApiHost",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyConfigs_CompanyKey",
                table: "CompanyConfigs",
                column: "CompanyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyConfigs_FrontHost",
                table: "CompanyConfigs",
                column: "FrontHost",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyConfigs");

            migrationBuilder.DropTable(
                name: "SyncJobs");
        }
    }
}

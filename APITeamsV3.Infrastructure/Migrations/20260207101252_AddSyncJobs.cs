using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrontHost = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApiHost = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SmartConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GraphTenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GraphClientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GraphClientSecretRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultChannelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeetingPolicyMode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

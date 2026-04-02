using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HangfireJobId",
                table: "SyncJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPilotMode",
                table: "CompanyConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CompanyPilotSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    IdSeccion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPilotSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPilotSections_CompanyConfigs_CompanyConfigId",
                        column: x => x.CompanyConfigId,
                        principalTable: "CompanyConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_Status",
                table: "SyncJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPilotSections_CompanyConfigId_IdSeccion",
                table: "CompanyPilotSections",
                columns: new[] { "CompanyConfigId", "IdSeccion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPilotSections");

            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_Status",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "HangfireJobId",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "IsPilotMode",
                table: "CompanyConfigs");
        }
    }
}

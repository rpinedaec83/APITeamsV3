using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClientColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiClientId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiScopes",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiClientId",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "ApiScopes",
                table: "CompanyConfigs");
        }
    }
}

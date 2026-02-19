using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpaTenantId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpaTenantId",
                table: "CompanyConfigs");
        }
    }
}

using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITeamsV3.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CentralDbContext))]
    [Migration("20260412133000_AddCTeamsAutomaticSyncStatusSp")]
    public partial class AddCTeamsAutomaticSyncStatusSp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("SqlServer"))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.cTeamsAutomaticSyncStatus', N'P') IS NULL
                    EXEC('CREATE PROCEDURE dbo.cTeamsAutomaticSyncStatus AS BEGIN SET NOCOUNT ON; SELECT CAST(0 AS bit) AS IsAutomaticSyncRunning; END');

                EXEC('
                ALTER PROCEDURE dbo.cTeamsAutomaticSyncStatus
                    @CompanyKey NVARCHAR(100)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT CAST(
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM [dbo].[CompanyConfigs] c
                                INNER JOIN [dbo].[SyncScheduleExecutions] e
                                    ON e.CompanyConfigId = c.Id
                                WHERE c.IsActive = 1
                                  AND LOWER(c.CompanyKey) = LOWER(@CompanyKey)
                                  AND LOWER(ISNULL(e.TriggerSource, '''')) = ''schedulerservice''
                                  AND e.CompletedAtUtc IS NULL
                                  AND LOWER(ISNULL(e.Status, '''')) IN (''started'', ''processing'', ''running'')
                            )
                            THEN 1 ELSE 0
                        END
                    AS bit) AS IsAutomaticSyncRunning;
                END
                ');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("SqlServer"))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.cTeamsAutomaticSyncStatus', N'P') IS NOT NULL
                    DROP PROCEDURE dbo.cTeamsAutomaticSyncStatus;
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddScheduleMissedRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastMissedRuns",
                schema: "app",
                table: "campaign_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalMissedRuns",
                schema: "app",
                table: "campaign_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMissedRuns",
                schema: "app",
                table: "campaign_schedules");

            migrationBuilder.DropColumn(
                name: "TotalMissedRuns",
                schema: "app",
                table: "campaign_schedules");
        }
    }
}

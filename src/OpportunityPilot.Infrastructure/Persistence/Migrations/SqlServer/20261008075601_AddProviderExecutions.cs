using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddProviderExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                schema: "app",
                table: "outreach_drafts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "provider_executions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectVersion = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Receipt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SafeFailure = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_executions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_provider_executions_OwnerId_IdempotencyKey",
                schema: "app",
                table: "provider_executions",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_executions_OwnerId_SubjectId",
                schema: "app",
                table: "provider_executions",
                columns: new[] { "OwnerId", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_executions",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "SentAt",
                schema: "app",
                table: "outreach_drafts");
        }
    }
}

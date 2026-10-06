using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddOutreachDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outreach_drafts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    RecipientVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedVersion = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClaimsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outreach_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outreach_drafts_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "app",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outreach_drafts_OpportunityId_Channel",
                schema: "app",
                table: "outreach_drafts",
                columns: new[] { "OpportunityId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outreach_drafts_OwnerId_OpportunityId_UpdatedAt",
                schema: "app",
                table: "outreach_drafts",
                columns: new[] { "OwnerId", "OpportunityId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outreach_drafts_OwnerId_State_UpdatedAt",
                schema: "app",
                table: "outreach_drafts",
                columns: new[] { "OwnerId", "State", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outreach_drafts",
                schema: "app");
        }
    }
}

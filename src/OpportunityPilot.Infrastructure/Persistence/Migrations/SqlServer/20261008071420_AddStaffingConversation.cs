using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddStaffingConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staffing_meetings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Invitees = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Agenda = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Receipt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_meetings_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffing_messages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Counterpart = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedVersion = table.Column<int>(type: "int", nullable: true),
                    Receipt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Intent = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_messages_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_meetings_DealId",
                schema: "app",
                table: "staffing_meetings",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_meetings_OwnerId_StartsAt",
                schema: "app",
                table: "staffing_meetings",
                columns: new[] { "OwnerId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_messages_DealId",
                schema: "app",
                table: "staffing_messages",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_messages_OwnerId_DealId_CreatedAt",
                schema: "app",
                table: "staffing_messages",
                columns: new[] { "OwnerId", "DealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_messages_OwnerId_State",
                schema: "app",
                table: "staffing_messages",
                columns: new[] { "OwnerId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staffing_meetings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_messages",
                schema: "app");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddSalesPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_projects",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Buyer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_bids",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DeliveryDays = table.Column<int>(type: "integer", nullable: false),
                    Proposal = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ClaimsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedVersion = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_bids_sales_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "app",
                        principalTable: "sales_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bids_OwnerId_ProjectId_UpdatedAt",
                schema: "app",
                table: "sales_bids",
                columns: new[] { "OwnerId", "ProjectId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bids_ProjectId",
                schema: "app",
                table: "sales_bids",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_projects_OwnerId_Source_ExternalId",
                schema: "app",
                table: "sales_projects",
                columns: new[] { "OwnerId", "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_projects_OwnerId_UpdatedAt",
                schema: "app",
                table: "sales_projects",
                columns: new[] { "OwnerId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_bids",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sales_projects",
                schema: "app");
        }
    }
}

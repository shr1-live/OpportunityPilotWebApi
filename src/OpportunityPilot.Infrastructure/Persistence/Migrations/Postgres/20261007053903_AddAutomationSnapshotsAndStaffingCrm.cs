using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddAutomationSnapshotsAndStaffingCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignVersion",
                schema: "app",
                table: "research_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CriteriaSnapshotJson",
                schema: "app",
                table: "research_jobs",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ProfileSnapshotJson",
                schema: "app",
                table: "research_jobs",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "ProfileVersion",
                schema: "app",
                table: "research_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "campaign_schedules",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CadenceMinutes = table.Column<int>(type: "integer", nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Paused = table.Column<bool>(type: "boolean", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastQueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSafeError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_schedules_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_versions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StructuredDataJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_versions_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "app",
                        principalTable: "profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffing_accounts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    Industry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staffing_contacts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_contacts_staffing_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "app",
                        principalTable: "staffing_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffing_deals",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StageBeforeHold = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NextAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextActionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_deals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_deals_staffing_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "app",
                        principalTable: "staffing_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffing_deals_staffing_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "app",
                        principalTable: "staffing_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staffing_deal_activities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_deal_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_deal_activities_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_schedules_CampaignId",
                schema: "app",
                table: "campaign_schedules",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_schedules_OwnerId_CampaignId",
                schema: "app",
                table: "campaign_schedules",
                columns: new[] { "OwnerId", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_schedules_Paused_NextRunAt",
                schema: "app",
                table: "campaign_schedules",
                columns: new[] { "Paused", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_profile_versions_OwnerId_ProfileId_Version",
                schema: "app",
                table: "profile_versions",
                columns: new[] { "OwnerId", "ProfileId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_versions_ProfileId",
                schema: "app",
                table: "profile_versions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_accounts_OwnerId_Domain",
                schema: "app",
                table: "staffing_accounts",
                columns: new[] { "OwnerId", "Domain" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_accounts_OwnerId_UpdatedAt",
                schema: "app",
                table: "staffing_accounts",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_contacts_AccountId",
                schema: "app",
                table: "staffing_contacts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_contacts_OwnerId_AccountId_Name",
                schema: "app",
                table: "staffing_contacts",
                columns: new[] { "OwnerId", "AccountId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deal_activities_DealId",
                schema: "app",
                table: "staffing_deal_activities",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deal_activities_OwnerId_DealId_OccurredAt",
                schema: "app",
                table: "staffing_deal_activities",
                columns: new[] { "OwnerId", "DealId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deals_AccountId",
                schema: "app",
                table: "staffing_deals",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deals_ContactId",
                schema: "app",
                table: "staffing_deals",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deals_OwnerId_Source_ExternalReference",
                schema: "app",
                table: "staffing_deals",
                columns: new[] { "OwnerId", "Source", "ExternalReference" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_deals_OwnerId_Stage_UpdatedAt",
                schema: "app",
                table: "staffing_deals",
                columns: new[] { "OwnerId", "Stage", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_schedules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "profile_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_deal_activities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_deals",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_contacts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_accounts",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "CampaignVersion",
                schema: "app",
                table: "research_jobs");

            migrationBuilder.DropColumn(
                name: "CriteriaSnapshotJson",
                schema: "app",
                table: "research_jobs");

            migrationBuilder.DropColumn(
                name: "ProfileSnapshotJson",
                schema: "app",
                table: "research_jobs");

            migrationBuilder.DropColumn(
                name: "ProfileVersion",
                schema: "app",
                table: "research_jobs");
        }
    }
}

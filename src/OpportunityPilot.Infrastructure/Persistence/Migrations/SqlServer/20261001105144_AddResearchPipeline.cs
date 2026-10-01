using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddResearchPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WeightsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultLimit = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaigns_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "app",
                        principalTable: "profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetrievedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExtractionMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Committed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_batches_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApplyUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Coverage = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OutcomeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GapsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GapsCount = table.Column<int>(type: "int", nullable: false),
                    LastResearchJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_opportunities_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_jobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SafeError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_jobs_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    PermissionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SafeError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sources_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "app",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activities_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "app",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_evidence",
                schema: "app",
                columns: table => new
                {
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_evidence", x => new { x.OpportunityId, x.EvidenceId });
                    table.ForeignKey(
                        name: "FK_opportunity_evidence_evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalSchema: "app",
                        principalTable: "evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_opportunity_evidence_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "app",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_events_research_jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "app",
                        principalTable: "research_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_items",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_items_sources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "app",
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_OpportunityId_OccurredAt",
                schema: "app",
                table: "activities",
                columns: new[] { "OpportunityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_OwnerId",
                schema: "app",
                table: "activities",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_OwnerId_CreatedAt",
                schema: "app",
                table: "campaigns",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_ProfileId",
                schema: "app",
                table: "campaigns",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_CampaignId_SourceId_ContentHash",
                schema: "app",
                table: "evidence",
                columns: new[] { "CampaignId", "SourceId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_OwnerId",
                schema: "app",
                table: "evidence",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_CampaignId",
                schema: "app",
                table: "import_batches",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_OwnerId_CreatedAt",
                schema: "app",
                table: "import_batches",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_CampaignId_DedupeKey",
                schema: "app",
                table: "opportunities",
                columns: new[] { "CampaignId", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_OwnerId_CampaignId_Score",
                schema: "app",
                table: "opportunities",
                columns: new[] { "OwnerId", "CampaignId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_OwnerId_Status_UpdatedAt",
                schema: "app",
                table: "opportunities",
                columns: new[] { "OwnerId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_evidence_EvidenceId",
                schema: "app",
                table: "opportunity_evidence",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_research_events_JobId_At",
                schema: "app",
                table: "research_events",
                columns: new[] { "JobId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_research_jobs_OwnerId_CampaignId_CreatedAt",
                schema: "app",
                table: "research_jobs",
                columns: new[] { "OwnerId", "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_research_jobs_State_CreatedAt",
                schema: "app",
                table: "research_jobs",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_research_jobs_active_campaign",
                schema: "app",
                table: "research_jobs",
                column: "CampaignId",
                unique: true,
                filter: "[State] IN (N'Queued', N'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_source_items_SourceId_ExternalId",
                schema: "app",
                table: "source_items",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_source_items_SourceId_UpdatedAt",
                schema: "app",
                table: "source_items",
                columns: new[] { "SourceId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sources_CampaignId",
                schema: "app",
                table: "sources",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_sources_OwnerId_CampaignId_CreatedAt",
                schema: "app",
                table: "sources",
                columns: new[] { "OwnerId", "CampaignId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "import_batches",
                schema: "app");

            migrationBuilder.DropTable(
                name: "opportunity_evidence",
                schema: "app");

            migrationBuilder.DropTable(
                name: "research_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "source_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "evidence",
                schema: "app");

            migrationBuilder.DropTable(
                name: "opportunities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "research_jobs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sources",
                schema: "app");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "app");
        }
    }
}

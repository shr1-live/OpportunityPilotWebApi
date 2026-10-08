using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddStaffingPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staffing_candidates",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YearsExperience = table.Column<double>(type: "float", nullable: true),
                    Availability = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RateAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RateCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    RateUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ResumeText = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    ResumeVersion = table.Column<int>(type: "int", nullable: false),
                    Consent = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ConsentRecordedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsentEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ShareableFields = table.Column<int>(type: "int", nullable: false),
                    NotifyByEmail = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staffing_rate_cards",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CardVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_rate_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staffing_submissions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedFields = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 40000, nullable: false),
                    CandidateVersion = table.Column<int>(type: "int", nullable: false),
                    ResumeVersion = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedVersion = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Receipt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_submissions_staffing_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "app",
                        principalTable: "staffing_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staffing_submissions_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffing_proposals",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateCardVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedVersion = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Receipt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_proposals_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffing_proposals_staffing_rate_cards_RateCardId",
                        column: x => x.RateCardId,
                        principalSchema: "app",
                        principalTable: "staffing_rate_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staffing_feedback",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SharedWithCandidate = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_feedback_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffing_feedback_staffing_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalSchema: "app",
                        principalTable: "staffing_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staffing_interviews",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CandidateNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CandidateNotification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CandidateNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_interviews_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffing_interviews_staffing_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalSchema: "app",
                        principalTable: "staffing_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staffing_offers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CandidatePay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlacementValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Contract = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContractVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SignatureProvider = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignedDocumentReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffing_offers_staffing_deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "app",
                        principalTable: "staffing_deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffing_offers_staffing_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalSchema: "app",
                        principalTable: "staffing_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_candidates_OwnerId_UpdatedAt",
                schema: "app",
                table: "staffing_candidates",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_feedback_DealId",
                schema: "app",
                table: "staffing_feedback",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_feedback_OwnerId_SubmissionId_RecordedAt",
                schema: "app",
                table: "staffing_feedback",
                columns: new[] { "OwnerId", "SubmissionId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_feedback_SubmissionId",
                schema: "app",
                table: "staffing_feedback",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_interviews_DealId",
                schema: "app",
                table: "staffing_interviews",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_interviews_OwnerId_DealId_Round",
                schema: "app",
                table: "staffing_interviews",
                columns: new[] { "OwnerId", "DealId", "Round" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_interviews_OwnerId_ScheduledAt",
                schema: "app",
                table: "staffing_interviews",
                columns: new[] { "OwnerId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_interviews_SubmissionId",
                schema: "app",
                table: "staffing_interviews",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_offers_DealId",
                schema: "app",
                table: "staffing_offers",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_offers_OwnerId_DealId",
                schema: "app",
                table: "staffing_offers",
                columns: new[] { "OwnerId", "DealId" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_offers_SubmissionId",
                schema: "app",
                table: "staffing_offers",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_proposals_DealId",
                schema: "app",
                table: "staffing_proposals",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_proposals_OwnerId_DealId",
                schema: "app",
                table: "staffing_proposals",
                columns: new[] { "OwnerId", "DealId" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_proposals_RateCardId",
                schema: "app",
                table: "staffing_proposals",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_rate_cards_OwnerId_Status",
                schema: "app",
                table: "staffing_rate_cards",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_submissions_CandidateId",
                schema: "app",
                table: "staffing_submissions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_submissions_DealId",
                schema: "app",
                table: "staffing_submissions",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_staffing_submissions_OwnerId_CandidateId",
                schema: "app",
                table: "staffing_submissions",
                columns: new[] { "OwnerId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_staffing_submissions_OwnerId_DealId",
                schema: "app",
                table: "staffing_submissions",
                columns: new[] { "OwnerId", "DealId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staffing_feedback",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_interviews",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_offers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_proposals",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_submissions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_rate_cards",
                schema: "app");

            migrationBuilder.DropTable(
                name: "staffing_candidates",
                schema: "app");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPilot.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddProviderResearchQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "upwork_opportunities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderJobId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BudgetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BudgetMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ExperienceLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConnectsRequired = table.Column<int>(type: "integer", nullable: true),
                    AvailableConnectsAtReview = table.Column<int>(type: "integer", nullable: true),
                    PaymentVerified = table.Column<bool>(type: "boolean", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SalesProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upwork_opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_upwork_opportunities_sales_projects_SalesProjectId",
                        column: x => x.SalesProjectId,
                        principalSchema: "app",
                        principalTable: "sales_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "wellfound_jobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderJobId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyLogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemoteType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    EquityMin = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    EquityMax = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    ExperienceLevel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Industry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FundingStage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmployeeCount = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisaSponsorship = table.Column<bool>(type: "boolean", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApplyUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    SkillsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wellfound_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wellfound_applications",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderApplicationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CandidateName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FitScore = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wellfound_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wellfound_applications_wellfound_jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "app",
                        principalTable: "wellfound_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wellfound_activities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProviderConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wellfound_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wellfound_activities_wellfound_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "app",
                        principalTable: "wellfound_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wellfound_activities_wellfound_jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "app",
                        principalTable: "wellfound_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_upwork_opportunities_OwnerId_ProviderJobId",
                schema: "app",
                table: "upwork_opportunities",
                columns: new[] { "OwnerId", "ProviderJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upwork_opportunities_OwnerId_State_ObservedAt",
                schema: "app",
                table: "upwork_opportunities",
                columns: new[] { "OwnerId", "State", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_upwork_opportunities_SalesProjectId",
                schema: "app",
                table: "upwork_opportunities",
                column: "SalesProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_activities_ApplicationId",
                schema: "app",
                table: "wellfound_activities",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_activities_JobId",
                schema: "app",
                table: "wellfound_activities",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_activities_OwnerId_OccurredAt",
                schema: "app",
                table: "wellfound_activities",
                columns: new[] { "OwnerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_applications_JobId",
                schema: "app",
                table: "wellfound_applications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_applications_OwnerId_ProviderApplicationId",
                schema: "app",
                table: "wellfound_applications",
                columns: new[] { "OwnerId", "ProviderApplicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_applications_OwnerId_State_UpdatedAt",
                schema: "app",
                table: "wellfound_applications",
                columns: new[] { "OwnerId", "State", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_jobs_OwnerId_ProviderJobId",
                schema: "app",
                table: "wellfound_jobs",
                columns: new[] { "OwnerId", "ProviderJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wellfound_jobs_OwnerId_Scope_State_PostedAt",
                schema: "app",
                table: "wellfound_jobs",
                columns: new[] { "OwnerId", "Scope", "State", "PostedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "upwork_opportunities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "wellfound_activities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "wellfound_applications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "wellfound_jobs",
                schema: "app");
        }
    }
}

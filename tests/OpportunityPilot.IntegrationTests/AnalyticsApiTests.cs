using System.Net;
using System.Text.Json;

namespace OpportunityPilot.IntegrationTests;

public class AnalyticsApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Analytics_requires_user_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/analytics/overview?workspace=Candidate");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Empty_candidate_and_sales_workspaces_return_honest_shapes()
    {
        var client = factory.ClientFor($"analytics-empty-{Guid.NewGuid():N}@example.test");

        var candidate = await client.GetJson("/api/v1/analytics/overview?workspace=Candidate&days=30");
        Assert.Equal("Candidate", candidate.Str("workspace"));
        Assert.Equal(0, candidate.Int("campaignCount"));
        Assert.Equal(0, candidate.GetProperty("kpis").Int("found"));
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("kpis").GetProperty("qualifyRate").ValueKind);
        Assert.Equal(14, candidate.GetProperty("applicationsPerDay").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("qualifiedByIndustry").ValueKind);

        var sales = await client.GetJson("/api/v1/analytics/overview?workspace=Sales&days=1");
        Assert.Equal("Sales", sales.Str("workspace"));
        Assert.Equal(JsonValueKind.Null, sales.GetProperty("kpis").GetProperty("applied").ValueKind);
        Assert.Equal(JsonValueKind.Null, sales.GetProperty("applicationsPerDay").ValueKind);
        Assert.Equal(0, sales.GetProperty("qualifiedByIndustry").GetArrayLength());
    }

    [Theory]
    [InlineData("Unknown", 30)]
    [InlineData("Candidate", 0)]
    [InlineData("Sales", 366)]
    public async Task Invalid_query_values_return_field_errors(string workspace, int days)
    {
        var client = factory.ClientFor($"analytics-invalid-{Guid.NewGuid():N}@example.test");
        var response = await client.GetAsync($"/api/v1/analytics/overview?workspace={workspace}&days={days}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(workspace == "Unknown" ? "workspace" : "days", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Campaign_counts_are_owner_and_workspace_scoped()
    {
        var owner = factory.ClientFor($"analytics-owner-{Guid.NewGuid():N}@example.test");
        var other = factory.ClientFor($"analytics-other-{Guid.NewGuid():N}@example.test");
        await ResearchApi.CreateCampaignAsync(owner, "Job", new { keywords = new[] { "engineer" } }, "Owner job");
        await ResearchApi.CreateCampaignAsync(owner, "Customer", new { industries = new[] { "SaaS" } }, "Owner sales");
        await ResearchApi.CreateCampaignAsync(other, "Job", new { keywords = new[] { "designer" } }, "Other job");

        var candidate = await owner.GetJson("/api/v1/analytics/overview?workspace=Candidate");
        var sales = await owner.GetJson("/api/v1/analytics/overview?workspace=Sales");

        Assert.Equal(1, candidate.Int("campaignCount"));
        Assert.Equal(1, sales.Int("campaignCount"));
    }
}

using System.Net;
using System.Net.Http.Json;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// The release smoke (S12): one owner goes from campaign to research to approval to a reviewed draft to a follow-up, on
/// a real Postgres with migrations applied. If this passes, the core pipeline works end to end.
/// </summary>
public class SmokeFlowTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Postings = """
        Senior .NET Engineer
        Company: Acme
        Location: Pune
        URL: https://jobs.example/acme-1
        We build APIs in C# on ASP.NET Core. 3-5 years of experience.
        """;

    [Fact]
    public async Task Campaign_to_research_to_approval_to_draft_to_follow_up()
    {
        var user = PostgresApiFactory.ClientFor(factory, "smoke@example.test");

        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job",
            new { keywords = new[] { ".NET" }, requiredSkills = new[] { "C#" }, locations = new[] { "Pune" } }, autoSuggestMinScore: 1);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Postings);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(factory.Services);
        Assert.Equal("Completed", (await ResearchApi.JobAsync(user, jobId)).Str("state"));

        var suggested = (await user.GetJson("/api/v1/approvals")).GetProperty("items").EnumerateArray().ToList();
        var opportunityId = Assert.Single(suggested).GetProperty("opportunityId").GetGuid();
        var decided = await (await user.PostAsJsonAsync("/api/v1/approvals/decide", new { approve = new[] { opportunityId } })).Json(HttpStatusCode.OK);
        Assert.Equal(1, decided.Int("approved"));
        Assert.Equal("Shortlisted", (await user.GetJson($"/api/v1/opportunities/{opportunityId}")).Str("status"));

        var draft = await (await user.PostAsJsonAsync($"/api/v1/opportunities/{opportunityId}/drafts", new { channel = "CoverNote" })).Json(HttpStatusCode.Created);
        Assert.Equal("Draft", draft.Str("state"));
        // Claims drawn from the profile name the exact profile version they came from.
        Assert.All(draft.GetProperty("claims").EnumerateArray().Where(c => c.Str("basis").StartsWith("Profile")), c => Assert.Matches(@"^Profile v\d+$", c.Str("basis")));
        Assert.Contains(draft.GetProperty("claims").EnumerateArray(), c => c.Str("basis").StartsWith("Profile v"));

        var followUp = await (await user.PostAsJsonAsync($"/api/v1/opportunities/{opportunityId}/next-actions",
            new { kind = "FollowUp", note = "Check the application", dueAt = "2026-10-15T09:00:00Z", timeZone = "Asia/Kolkata" })).Json(HttpStatusCode.Created);
        Assert.Equal("Open", followUp.Str("state"));
        Assert.Contains((await user.GetJson("/api/v1/next-actions")).EnumerateArray(), n => n.Id() == followUp.Id());
    }

    private const string Companies = """
        Northwind Integrations
        Website: https://northwind.example
        Location: London
        Industry: Systems integration
        Northwind implements Dynamics 365 for retailers and is hiring .NET developers after a new funding round.
        """;

    [Theory]
    [InlineData("Partner")]
    [InlineData("Investor")]
    [InlineData("Freelance")]
    public async Task Every_sales_mode_runs_research_and_scores_with_evidence(string mode)
    {
        var user = PostgresApiFactory.ClientFor(factory, $"smoke-{mode.ToLowerInvariant()}@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, mode,
            new { keywords = new[] { "Dynamics 365" }, industries = new[] { "Systems integration" }, problems = new[] { ".NET" }, signals = new[] { "funding" }, locations = new[] { "London" } });
        Assert.Equal(mode, campaign.Str("mode"));
        await ResearchApi.AddPasteAsync(user, campaign.Id(), Companies);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await PostgresApiFactory.RunResearchAsync(factory.Services);
        Assert.Equal("Completed", (await ResearchApi.JobAsync(user, jobId)).Str("state"));

        var item = Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
        Assert.Equal(mode, item.Str("mode"));
        Assert.Equal("Northwind Integrations", item.Str("organization"));
        Assert.True(item.Int("score") > 0, "a matching company should score above zero");
        var detail = await user.GetJson($"/api/v1/opportunities/{item.Id()}");
        Assert.NotEmpty(detail.GetProperty("evidence").EnumerateArray());
        Assert.NotEmpty(detail.GetProperty("breakdown").EnumerateArray());
    }
}

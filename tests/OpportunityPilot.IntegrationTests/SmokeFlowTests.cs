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

        var followUp = await (await user.PostAsJsonAsync($"/api/v1/opportunities/{opportunityId}/next-actions",
            new { kind = "FollowUp", note = "Check the application", dueAt = "2026-10-15T09:00:00Z", timeZone = "Asia/Kolkata" })).Json(HttpStatusCode.Created);
        Assert.Equal("Open", followUp.Str("state"));
        Assert.Contains((await user.GetJson("/api/v1/next-actions")).EnumerateArray(), n => n.Id() == followUp.Id());
    }
}

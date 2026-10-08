using System.Net;
using System.Net.Http.Json;

namespace OpportunityPilot.IntegrationTests;

public class SalesDemoApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task The_demo_builds_every_sales_mode_and_staffing_records_and_reset_removes_only_demo_data()
    {
        var user = PostgresApiFactory.ClientFor(factory, "sales-demo@example.test");
        var mine = await ResearchApi.CreateCampaignAsync(user, "Customer", new { keywords = new[] { "mine" } }, name: "My own campaign");

        var started = await (await user.PostAsync("/api/v1/demo/sales", null)).Json(HttpStatusCode.OK);
        Assert.Equal(4, started.Int("campaigns"));
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync("/api/v1/demo/sales/finish", null)).StatusCode); // research not run yet
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync("/api/v1/demo/sales", null)).StatusCode);        // already exists

        await PostgresApiFactory.RunResearchAsync(factory.Services);
        var done = await (await user.PostAsync("/api/v1/demo/sales/finish", null)).Json(HttpStatusCode.OK);
        Assert.True(done.GetProperty("researchDone").GetBoolean());
        Assert.Equal(8, done.Int("opportunities"));
        Assert.Equal(4, done.Int("shortlisted"));
        Assert.Equal(4, done.Int("drafts"));
        Assert.Equal(1, done.Int("staffingDeals"));
        var again = await (await user.PostAsync("/api/v1/demo/sales/finish", null)).Json(HttpStatusCode.OK);
        Assert.Equal(4, again.Int("drafts")); // finishing twice changes nothing

        var modes = (await user.GetJson("/api/v1/campaigns")).EnumerateArray().Where(c => c.Str("name").StartsWith("Demo — ")).Select(c => c.Str("mode")).OrderBy(m => m).ToList();
        Assert.Equal(["Customer", "Freelance", "Investor", "Partner"], modes);
        var deal = (await user.GetJson("/api/v1/staffing/deals")).EnumerateArray().Single();
        Assert.Equal("Won", deal.Str("stage"));
        var staffingKpis = await user.GetJson("/api/v1/staffing/kpis");
        Assert.Equal(1, staffingKpis.Int("placements"));
        Assert.Equal(1, staffingKpis.Int("contractsSigned"));
        var kpis = await user.GetJson("/api/v1/analytics/overview?workspace=Sales&days=30");
        Assert.Equal(4, kpis.GetProperty("kpis").Int("shortlisted"));
        Assert.Equal(1, kpis.GetProperty("outreach").Int("draftsApproved"));

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync("/api/v1/demo/sales")).StatusCode);
        var after = await user.GetJson("/api/v1/demo/sales");
        Assert.False(after.GetProperty("exists").GetBoolean());
        Assert.Empty((await user.GetJson("/api/v1/staffing/deals")).EnumerateArray());
        Assert.Empty((await user.GetJson("/api/v1/staffing/candidates")).EnumerateArray());
        var left = (await user.GetJson("/api/v1/campaigns")).EnumerateArray().Select(c => c.Id()).ToList();
        Assert.Equal([mine.Id()], left);
        Assert.DoesNotContain((await user.GetJson("/api/v1/profiles")).EnumerateArray(), p => p.Str("name").StartsWith("Demo — "));

        // Replayable.
        Assert.Equal(4, (await (await user.PostAsync("/api/v1/demo/sales", null)).Json(HttpStatusCode.OK)).Int("campaigns"));
    }
}

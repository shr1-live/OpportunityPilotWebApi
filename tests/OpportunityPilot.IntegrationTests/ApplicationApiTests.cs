using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpportunityPilot.IntegrationTests;

public class ApplicationApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private record AgentKeyResponse(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt);
    private record CreatedKeyResponse(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt, string Key);
    private record ReportResponse(int Accepted);
    private record ApplicationResponse(Guid Id, string Platform, string ExternalJobId, string JobUrl, string Title, string Company,
        string? Location, string Status, string? Detail, DateTime OccurredAt, DateTime UpdatedAt);
    private record PageResponse(int Total, List<ApplicationResponse> Items);
    private record SummaryResponse(int Applied, int AppliedLast7Days, int NeedsManual, int DryRun, int Skipped, int Failed, DateTime? LastActivityAt);
    private record OverviewResponse(int Profiles, int Applied, int NeedsManual);

    private static object Item(string jobId, string status, DateTime occurredAt, string platform = "LinkedIn", string? jobUrl = null) => new
    {
        platform,
        externalJobId = jobId,
        jobUrl = jobUrl ?? $"https://www.linkedin.com/jobs/view/{jobId}/",
        title = $"Backend Engineer {jobId}",
        company = "Acme",
        location = "Pune",
        status,
        detail = status == "NeedsManual" ? "Unanswered question: notice period" : null,
        occurredAt
    };

    private async Task<CreatedKeyResponse> CreateKeyAsync(HttpClient user, string name = "Laptop")
    {
        var response = await user.PostAsJsonAsync("/api/v1/agent-keys", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedKeyResponse>())!;
    }

    private HttpClient AgentClient(string key)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Agent-Key", key);
        return client;
    }

    private static Task<HttpResponseMessage> ReportAsync(HttpClient client, params object[] items) =>
        client.PostAsJsonAsync("/api/v1/applications/report", new { items });

    [Fact]
    public async Task Created_key_is_shown_once_and_list_shows_only_the_prefix()
    {
        var user = factory.ClientFor("keys@example.test");

        var created = await CreateKeyAsync(user, "  Home laptop  ");

        Assert.StartsWith("opk_", created.Key);
        Assert.Equal("Home laptop", created.Name);
        Assert.Equal(created.Key[..12], created.Prefix);

        var listBody = await user.GetStringAsync("/api/v1/agent-keys");
        var listed = JsonSerializer.Deserialize<List<AgentKeyResponse>>(listBody, JsonSerializerOptions.Web)!;
        Assert.Contains(listed, k => k.Id == created.Id && k.Prefix == created.Prefix);
        Assert.DoesNotContain(created.Key, listBody);
        Assert.DoesNotContain("\"key\"", listBody);
    }

    [Fact]
    public async Task Agent_report_is_listed_newest_first_and_counted_in_summary_and_overview()
    {
        var user = factory.ClientFor("reporter@example.test");
        var agent = AgentClient((await CreateKeyAsync(user)).Key);
        var now = DateTime.UtcNow;

        var response = await ReportAsync(agent,
            Item("1001", "Applied", now.AddHours(-2)),
            Item("1002", "NeedsManual", now.AddHours(-1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<ReportResponse>())!.Accepted);

        var page = (await user.GetFromJsonAsync<PageResponse>("/api/v1/applications"))!;
        Assert.Equal(2, page.Total);
        Assert.Equal(["1002", "1001"], page.Items.Select(i => i.ExternalJobId));
        Assert.Equal("NeedsManual", page.Items[0].Status);
        Assert.Equal("LinkedIn", page.Items[0].Platform);

        var filtered = (await user.GetFromJsonAsync<PageResponse>("/api/v1/applications?status=Applied&platform=LinkedIn"))!;
        Assert.Equal("1001", Assert.Single(filtered.Items).ExternalJobId);

        var summary = (await user.GetFromJsonAsync<SummaryResponse>("/api/v1/applications/summary"))!;
        Assert.Equal(1, summary.Applied);
        Assert.Equal(1, summary.AppliedLast7Days);
        Assert.Equal(1, summary.NeedsManual);
        Assert.Equal(0, summary.DryRun + summary.Skipped + summary.Failed);
        Assert.NotNull(summary.LastActivityAt);

        var overview = (await user.GetFromJsonAsync<OverviewResponse>("/api/v1/overview"))!;
        Assert.Equal(1, overview.Applied);
        Assert.Equal(1, overview.NeedsManual);

        var keys = (await user.GetFromJsonAsync<List<AgentKeyResponse>>("/api/v1/agent-keys"))!;
        Assert.NotNull(Assert.Single(keys).LastUsedAt);
    }

    [Fact]
    public async Task A_later_dry_run_does_not_downgrade_an_applied_job()
    {
        var user = factory.ClientFor("nodowngrade@example.test");
        var agent = AgentClient((await CreateKeyAsync(user)).Key);
        var now = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.OK, (await ReportAsync(agent, Item("2001", "Applied", now.AddMinutes(-30)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReportAsync(agent, Item("2001", "DryRun", now.AddMinutes(-5)))).StatusCode);

        var page = (await user.GetFromJsonAsync<PageResponse>("/api/v1/applications"))!;
        Assert.Equal("Applied", Assert.Single(page.Items).Status);
    }

    [Fact]
    public async Task Agent_keys_and_user_tokens_only_work_on_their_own_endpoints()
    {
        var user = factory.ClientFor("schemes@example.test");
        var agent = AgentClient((await CreateKeyAsync(user)).Key);

        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/agent-keys")).StatusCode);

        var asUser = await ReportAsync(user, Item("3001", "Applied", DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, asUser.StatusCode);

        var anonymous = await ReportAsync(factory.CreateClient(), Item("3001", "Applied", DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var bogus = await ReportAsync(AgentClient("opk_not-a-real-key"), Item("3001", "Applied", DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, bogus.StatusCode);
    }

    [Fact]
    public async Task Revoked_key_is_rejected()
    {
        var user = factory.ClientFor("revoke@example.test");
        var created = await CreateKeyAsync(user);
        var agent = AgentClient(created.Key);
        Assert.Equal(HttpStatusCode.OK, (await ReportAsync(agent, Item("4001", "Applied", DateTime.UtcNow))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/v1/agent-keys/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/v1/agent-keys/{created.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await ReportAsync(agent, Item("4002", "Applied", DateTime.UtcNow))).StatusCode);
        Assert.Empty((await user.GetFromJsonAsync<List<AgentKeyResponse>>("/api/v1/agent-keys"))!);
    }

    [Fact]
    public async Task Users_cannot_see_others_applications_or_revoke_their_keys()
    {
        var erin = factory.ClientFor("erin@example.test");
        var frank = factory.ClientFor("frank@example.test");
        var erinKey = await CreateKeyAsync(erin);
        Assert.Equal(HttpStatusCode.OK, (await ReportAsync(AgentClient(erinKey.Key), Item("5001", "Applied", DateTime.UtcNow))).StatusCode);

        var frankPage = (await frank.GetFromJsonAsync<PageResponse>("/api/v1/applications"))!;
        Assert.Equal(0, frankPage.Total);
        Assert.Equal(0, (await frank.GetFromJsonAsync<SummaryResponse>("/api/v1/applications/summary"))!.Applied);
        Assert.Empty((await frank.GetFromJsonAsync<List<AgentKeyResponse>>("/api/v1/agent-keys"))!);

        Assert.Equal(HttpStatusCode.NotFound, (await frank.DeleteAsync($"/api/v1/agent-keys/{erinKey.Id}")).StatusCode);
        Assert.Single((await erin.GetFromJsonAsync<List<AgentKeyResponse>>("/api/v1/agent-keys"))!);
    }

    [Fact]
    public async Task Invalid_report_returns_field_errors()
    {
        var user = factory.ClientFor("badreport@example.test");
        var agent = AgentClient((await CreateKeyAsync(user)).Key);

        var response = await ReportAsync(agent, Item("6001", "Applied", DateTime.UtcNow, jobUrl: "javascript:alert(1)"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), e => e.Name.Contains("jobUrl"));
        Assert.Equal(0, (await user.GetFromJsonAsync<PageResponse>("/api/v1/applications"))!.Total);
    }
}

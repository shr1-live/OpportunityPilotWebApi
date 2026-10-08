using System.Net;
using System.Net.Http.Json;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Agent-key scopes and the owner's security event log.</summary>
public class SecurityApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task A_scoped_agent_key_reaches_only_its_own_endpoints_and_key_events_are_logged()
    {
        var user = PostgresApiFactory.ClientFor(factory, "security-scopes@example.test");
        var created = await (await user.PostAsJsonAsync("/api/v1/agent-keys", new { name = "Shortlist only", scopes = new[] { "Shortlist" } })).Json(HttpStatusCode.Created);
        Assert.Equal(["Shortlist"], created.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()));

        var agent = factory.CreateClient();
        agent.DefaultRequestHeaders.Add("X-Agent-Key", created.Str("key"));
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/api/v1/agent/shortlist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/v1/agent/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/v1/applications/report", new { items = Array.Empty<object>() })).StatusCode);
        // An agent key never satisfies the user policy.
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/profiles")).StatusCode);

        var bad = await user.PostAsJsonAsync("/api/v1/agent-keys", new { name = "Bad", scopes = new[] { "Everything" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/v1/agent-keys/{created.Id()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/agent/shortlist")).StatusCode);

        var events = (await user.GetJson("/api/v1/security/events")).EnumerateArray().Select(e => e.Str("type")).ToList();
        Assert.Contains("AgentKeyCreated", events);
        Assert.Contains("AgentKeyRevoked", events);
        var raw = await user.GetStringAsync("/api/v1/security/events");
        Assert.DoesNotContain(created.Str("key"), raw);

        // Another owner sees none of these events.
        var other = PostgresApiFactory.ClientFor(factory, "security-other@example.test");
        Assert.Empty((await other.GetJson("/api/v1/security/events")).EnumerateArray());
    }
}

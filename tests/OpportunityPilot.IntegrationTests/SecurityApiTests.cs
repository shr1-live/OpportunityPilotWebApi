using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Diagnostics_report_the_database_and_flag_a_schema_exposed_to_supabase_roles()
    {
        var user = PostgresApiFactory.ClientFor(factory, "security-diagnostics@example.test");
        var diag = await user.GetJson("/api/v1/diagnostics");
        var db = diag.GetProperty("database");
        Assert.True(db.GetProperty("reachable").GetBoolean());
        Assert.Equal(0, db.GetProperty("pendingMigrations").GetInt32());
        Assert.True(db.GetProperty("schemaExposure").GetProperty("checked").GetBoolean());
        Assert.Empty(db.GetProperty("schemaExposure").GetProperty("exposedTo").EnumerateArray());
        Assert.DoesNotContain("Password", diag.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", diag.GetRawText());

        // Simulate the Supabase misconfiguration: the anon role can use schema app.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<OpportunityPilot.Infrastructure.Persistence.AppDbContext>();
            await ctx.Database.ExecuteSqlRawAsync("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN CREATE ROLE anon NOLOGIN; END IF; END $$; GRANT USAGE ON SCHEMA app TO anon;");
        }
        try
        {
            var exposed = (await user.GetJson("/api/v1/diagnostics")).GetProperty("database").GetProperty("schemaExposure");
            Assert.Equal(["anon"], exposed.GetProperty("exposedTo").EnumerateArray().Select(r => r.GetString()));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<OpportunityPilot.Infrastructure.Persistence.AppDbContext>();
            await ctx.Database.ExecuteSqlRawAsync("REVOKE USAGE ON SCHEMA app FROM anon; DROP ROLE anon;");
        }
    }

    [Fact]
    public async Task Provider_readiness_states_support_credentials_and_only_stored_verification()
    {
        var user = PostgresApiFactory.ClientFor(factory, "providers@example.test");
        var rows = (await user.GetJson("/api/v1/providers/readiness")).EnumerateArray().ToDictionary(r => r.Str("key"));
        Assert.Contains("instahyre-agent", rows.Keys);
        Assert.Contains("wellfound", rows.Keys);
        Assert.Equal("Manual", rows["linkedin-messages"].Str("execution"));
        Assert.False(rows["job-boards"].GetProperty("credentialSet").GetBoolean());          // no JSearch key in tests
        Assert.Equal(JsonValueKind.Null, rows["linkedin-agent"].GetProperty("lastVerified").ValueKind); // nothing applied yet
        Assert.False(rows["instahyre-agent"].GetProperty("credentialSet").GetBoolean());     // no agent key yet

        await (await user.PostAsJsonAsync("/api/v1/agent-keys", new { name = "Laptop" })).Json(HttpStatusCode.Created);
        rows = (await user.GetJson("/api/v1/providers/readiness")).EnumerateArray().ToDictionary(r => r.Str("key"));
        Assert.True(rows["instahyre-agent"].GetProperty("credentialSet").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/providers/readiness")).StatusCode);
    }
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpportunityPilot.IntegrationTests;

/// <summary>No Docker needed: these never reach a database.</summary>
public class StartupGuardTests
{
    private sealed class ProductionFactory(Dictionary<string, string> settings) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        }
    }

    [Fact]
    public void Dev_bypass_cannot_be_enabled_outside_development()
    {
        using var factory = new ProductionFactory(new()
        {
            ["ConnectionStrings:Main"] = "Host=unused;Database=unused",
            ["Auth:DevBypass"] = "true",
            ["Auth:SupabaseUrl"] = "https://example.supabase.co"
        });
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("DevBypass", ex.ToString());
    }

    [Fact]
    public async Task Without_supabase_auth_the_api_starts_but_refuses_data_requests_with_setup_required()
    {
        using var factory = new ProductionFactory(new()
        {
            ["ConnectionStrings:Main"] = "Host=unused;Database=unused",
            ["Auth:SupabaseUrl"] = ""
        });
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

        var profiles = await client.GetAsync("/api/v1/profiles");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, profiles.StatusCode);
        Assert.Contains("Auth__SupabaseUrl", await profiles.Content.ReadAsStringAsync());

        // Even a token-shaped header gets nowhere.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/overview");
        request.Headers.Add("Authorization", "Bearer abc.def.ghi");
        request.Headers.Add("X-Dev-User", "intruder");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode);

        var caps = await client.GetStringAsync("/api/v1/capabilities");
        Assert.Contains("Auth__SupabaseUrl is not set.", caps);
    }

    [Fact]
    public async Task Without_a_connection_string_the_api_starts_and_reports_the_gap()
    {
        using var factory = new ProductionFactory(new()
        {
            ["ConnectionStrings:Main"] = "",
            ["Auth:SupabaseUrl"] = "https://example.supabase.co"
        });
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        var caps = await client.GetStringAsync("/api/v1/capabilities");
        Assert.Contains("ConnectionStrings__Main is not set.", caps);
        Assert.Contains("\"NotConfigured\"", caps);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Production-environment startup behaviour. No Docker needed: none of these reach a real database.</summary>
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

    private record GuestSession(string Token, DateTime ExpiresAt);

    private static ProductionFactory DemoMode() => new(new() { ["ConnectionStrings:Main"] = "", ["Auth:SupabaseUrl"] = "" });

    private static async Task<HttpClient> GuestClient(ProductionFactory factory)
    {
        var client = factory.CreateClient();
        var session = await (await client.PostAsync("/api/v1/auth/guest", null)).Content.ReadFromJsonAsync<GuestSession>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        return client;
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
    public async Task With_no_configuration_the_app_works_in_demo_mode()
    {
        using var factory = DemoMode();
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready")).StatusCode);

        var caps = JsonDocument.Parse(await anonymous.GetStringAsync("/api/v1/capabilities")).RootElement;
        Assert.True(caps.GetProperty("guestSignIn").GetBoolean());
        Assert.True(caps.GetProperty("temporaryStorage").GetBoolean());
        Assert.Equal(2, caps.GetProperty("setupRequired").GetArrayLength());

        var guest = await GuestClient(factory);
        var created = await guest.PostAsJsonAsync("/api/v1/profiles",
            new { type = "Candidate", name = "Demo profile", data = new { offer = "x" }, confirmed = false });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("\"profiles\":1", await guest.GetStringAsync("/api/v1/overview"));
    }

    [Fact]
    public async Task Guests_are_isolated_and_unsigned_or_forged_credentials_are_rejected()
    {
        using var factory = DemoMode();
        var alice = await GuestClient(factory);
        var bob = await GuestClient(factory);

        await alice.PostAsJsonAsync("/api/v1/profiles", new { type = "Services", name = "Alice", data = new { }, confirmed = false });
        Assert.Contains("\"profiles\":0", await bob.GetStringAsync("/api/v1/overview"));

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/profiles")).StatusCode);

        var forged = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles");
        var token = alice.DefaultRequestHeaders.Authorization!.Parameter!;
        forged.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");
        forged.Headers.Add("X-Dev-User", "alice");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(forged)).StatusCode);
    }

    [Fact]
    public async Task Guest_sign_in_is_off_once_supabase_is_configured()
    {
        using var factory = new ProductionFactory(new()
        {
            ["ConnectionStrings:Main"] = "",
            ["Auth:SupabaseUrl"] = "https://example.supabase.co"
        });
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/auth/guest", null)).StatusCode);
        var caps = JsonDocument.Parse(await client.GetStringAsync("/api/v1/capabilities")).RootElement;
        Assert.False(caps.GetProperty("guestSignIn").GetBoolean());
        Assert.True(caps.GetProperty("temporaryStorage").GetBoolean());
    }
}

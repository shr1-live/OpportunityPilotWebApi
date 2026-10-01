using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpportunityPilot.IntegrationTests;

public class ProfileApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private record ProfileResponse(Guid Id, string Type, string Name, JsonElement Data, int Version, DateTime? ConfirmedAt);

    private static object NewProfile(string name) => new
    {
        type = "Candidate",
        name,
        data = new { offer = "Fixed-scope .NET backend work", skills = "C#, EF Core" },
        confirmed = false
    };

    private static async Task<ProfileResponse> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/profiles", NewProfile(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProfileResponse>())!;
    }

    [Fact]
    public async Task Health_endpoints_respond_without_auth_and_report_database_ready()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.DoesNotContain("Password", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_endpoints_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/profiles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Created_profile_persists_with_structured_data_as_jsonb()
    {
        var client = factory.ClientFor("persist@example.test");
        var created = await CreateAsync(client, "Persisted profile");

        var fetched = await client.GetFromJsonAsync<ProfileResponse>($"/api/v1/profiles/{created.Id}");

        Assert.Equal(1, fetched!.Version);
        Assert.Equal("Fixed-scope .NET backend work", fetched.Data.GetProperty("offer").GetString());
    }

    [Fact]
    public async Task User_B_cannot_read_list_or_update_user_A_profile()
    {
        var alice = factory.ClientFor("alice@example.test");
        var bob = factory.ClientFor("bob@example.test");
        var aliceProfile = await CreateAsync(alice, "Alice only");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/profiles/{aliceProfile.Id}")).StatusCode);

        var bobList = await bob.GetFromJsonAsync<List<ProfileResponse>>("/api/v1/profiles");
        Assert.DoesNotContain(bobList!, p => p.Id == aliceProfile.Id);

        var update = await bob.PutAsJsonAsync($"/api/v1/profiles/{aliceProfile.Id}",
            new { name = "Hijacked", data = new { }, confirmed = true, expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var unchanged = await alice.GetFromJsonAsync<ProfileResponse>($"/api/v1/profiles/{aliceProfile.Id}");
        Assert.Equal("Alice only", unchanged!.Name);
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_409()
    {
        var client = factory.ClientFor("versions@example.test");
        var created = await CreateAsync(client, "Versioned");

        var first = await client.PutAsJsonAsync($"/api/v1/profiles/{created.Id}",
            new { name = "Versioned v2", data = new { offer = "v2" }, confirmed = true, expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var v2 = await first.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(2, v2!.Version);
        Assert.NotNull(v2.ConfirmedAt);

        var stale = await client.PutAsJsonAsync($"/api/v1/profiles/{created.Id}",
            new { name = "Versioned stale", data = new { }, confirmed = false, expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("correlationId", await stale.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_profile_returns_field_errors()
    {
        var client = factory.ClientFor("invalid@example.test");
        var response = await client.PostAsJsonAsync("/api/v1/profiles", new { type = "Candidate", name = "", data = new[] { 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name", body);
        Assert.Contains("data", body);
    }
}

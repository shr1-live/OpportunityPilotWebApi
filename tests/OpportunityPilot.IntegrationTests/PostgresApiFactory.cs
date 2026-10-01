using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container (requires Docker).
/// Uses the Development-only dev-bypass scheme so two synthetic users can be exercised without Supabase.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Postgres");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("ConnectionStrings:Main", _postgres.GetConnectionString());
        builder.UseSetting("Auth:DevBypass", "true");
        builder.UseSetting("Auth:SupabaseUrl", "");
    }

    public HttpClient ClientFor(string devUser)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", devUser);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

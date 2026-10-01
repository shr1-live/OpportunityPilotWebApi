using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Research;
using Testcontainers.PostgreSql;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container (requires Docker).
/// Uses the Development-only dev-bypass scheme so two synthetic users can be exercised without Supabase.
/// The hosted research processor is off; tests run queued jobs themselves with <see cref="RunResearchAsync"/>.
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
        builder.UseSetting("Research:ProcessorEnabled", "false");
    }

    public HttpClient ClientFor(string devUser) => ClientFor(this, devUser);

    public static HttpClient ClientFor(WebApplicationFactory<Program> factory, string devUser)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", devUser);
        return client;
    }

    /// <summary>Runs queued jobs to completion, one scope per job (as the hosted processor does). Returns how many ran.</summary>
    public Task<int> RunResearchAsync() => RunResearchAsync(Services);

    public static async Task<int> RunResearchAsync(IServiceProvider services)
    {
        var ran = 0;
        while (true)
        {
            await using var scope = services.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<IResearchRunner>().RunNextAsync(CancellationToken.None)) return ran;
            ran++;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

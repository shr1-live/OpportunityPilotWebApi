using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Infrastructure.Persistence;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, SetupState setup)
    {
        services.AddResearchInfrastructure(configuration);
        return services.AddPersistence(configuration, setup);
    }

    /// <summary>Safe outbound fetching and content parsing for research. Works the same with or without a database.</summary>
    public static IServiceCollection AddResearchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ResearchOptions>(configuration.GetSection(ResearchOptions.Section));
        // Not configurable on purpose: no setting can let the fetcher reach private addresses.
        services.TryAddSingleton<IFetchAddressPolicy, StrictFetchAddressPolicy>();
        services.AddSingleton<FetchConcurrency>();
        services.AddSingleton<IContentParser, ContentParser>();
        services.AddHttpClient<IWebFetcher, SafeFetcher>(SafeFetcher.ClientName, c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => SafeFetcher.CreateHandler(sp.GetRequiredService<IFetchAddressPolicy>()))
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, SetupState setup)
    {
        var provider = configuration[$"{DatabaseOptions.Section}:{nameof(DatabaseOptions.Provider)}"] ?? "SqlServer";
        if (provider is not ("SqlServer" or "Postgres"))
            throw new InvalidOperationException($"Setup required: Database:Provider must be SqlServer or Postgres (got '{provider}').");

        var connectionString = configuration["ConnectionStrings:Main"];
        if (!string.IsNullOrWhiteSpace(connectionString) && provider == "Postgres")
        {
            try { connectionString = PostgresConnectionString.Normalize(connectionString); }
            catch (InvalidOperationException) { connectionString = null; setup.DatabaseMissing("ConnectionStrings__Main is not a valid connection string, so data is kept in memory and resets when the server restarts."); }
        }
        else if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = null;
            setup.DatabaseMissing("ConnectionStrings__Main is not set, so data is kept in memory and resets when the server restarts.");
        }

        if (connectionString is null)
        {
            // Demo mode: the app works end to end, but nothing survives a restart.
            services.AddDbContext<InMemoryAppDbContext>(o => o.UseInMemoryDatabase("opportunitypilot-demo"));
            services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<InMemoryAppDbContext>());
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            return services;
        }

        switch (provider)
        {
            case "SqlServer":
                services.AddDbContext<SqlServerAppDbContext>(o => o.UseSqlServer(connectionString,
                    s => s.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqlServerAppDbContext>());
                break;
            case "Postgres":
                services.AddDbContext<PostgresAppDbContext>(o => o.UseNpgsql(connectionString,
                    s => s.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<PostgresAppDbContext>());
                break;
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }
}

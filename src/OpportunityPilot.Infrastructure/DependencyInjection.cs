using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.Infrastructure;

public static class DependencyInjection
{
    private const string NotConfigured = "Database is not configured. Set ConnectionStrings__Main on the server. See docs/SETUP.md.";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, SetupState setup)
    {
        var provider = configuration[$"{DatabaseOptions.Section}:{nameof(DatabaseOptions.Provider)}"] ?? "SqlServer";
        if (provider is not ("SqlServer" or "Postgres"))
            throw new InvalidOperationException($"Setup required: Database:Provider must be SqlServer or Postgres (got '{provider}').");

        var connectionString = configuration["ConnectionStrings:Main"];
        if (!string.IsNullOrWhiteSpace(connectionString) && provider == "Postgres")
        {
            try { connectionString = PostgresConnectionString.Normalize(connectionString); }
            catch (InvalidOperationException) { connectionString = null; setup.DatabaseMissing("ConnectionStrings__Main is not a valid connection string."); }
        }
        else if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = null;
            setup.DatabaseMissing("ConnectionStrings__Main is not set.");
        }

        if (connectionString is null)
        {
            // Start anyway so health and capabilities can report the gap; data requests answer 503.
            services.AddScoped<AppDbContext>(_ => throw new SetupRequiredException(NotConfigured));
            services.AddScoped<IAppDbContext>(_ => throw new SetupRequiredException(NotConfigured));
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

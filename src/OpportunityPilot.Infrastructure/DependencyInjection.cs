using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{DatabaseOptions.Section}:{nameof(DatabaseOptions.Provider)}"] ?? "SqlServer";
        var connectionString = configuration["ConnectionStrings:Main"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Setup required: ConnectionStrings:Main is not configured. See docs/SETUP.md.");

        switch (provider)
        {
            case "SqlServer":
                services.AddDbContext<SqlServerAppDbContext>(o => o.UseSqlServer(connectionString,
                    s => s.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqlServerAppDbContext>());
                break;
            case "Postgres":
                connectionString = PostgresConnectionString.Normalize(connectionString);
                services.AddDbContext<PostgresAppDbContext>(o => o.UseNpgsql(connectionString,
                    s => s.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<PostgresAppDbContext>());
                break;
            default:
                throw new InvalidOperationException($"Setup required: Database:Provider must be SqlServer or Postgres (got '{provider}').");
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }
}

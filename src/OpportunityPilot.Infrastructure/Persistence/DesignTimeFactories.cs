using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpportunityPilot.Infrastructure.Persistence;

// Used only by `dotnet ef`. Migrations are generated without touching a real database;
// `database update` reads ConnectionStrings__Main from the environment.

public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__Main")
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=OpportunityPilot;Trusted_Connection=True;TrustServerCertificate=True",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options);
}

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresAppDbContext>()
            .UseNpgsql(PostgresConnectionString.Normalize(Environment.GetEnvironmentVariable("ConnectionStrings__Main")
                ?? "Host=localhost;Database=opportunitypilot;Username=postgres;Password=postgres"),
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options);
}

using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Domain.Profiles;

namespace OpportunityPilot.Infrastructure.Persistence;

/// <summary>Local development against SQL Server LocalDB.</summary>
public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : AppDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // datetime2 has no kind; everything stored is UTC, so say so on the way back out.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }
}

/// <summary>Supabase PostgreSQL (cloud) and the integration-test container.</summary>
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Profile>().Property(p => p.StructuredDataJson).HasColumnType("jsonb");
    }
}

/// <summary>
/// Demo mode: used when no connection string is configured. Nothing survives a restart, which on a free
/// host happens whenever the instance sleeps. Reported through capabilities so it is never mistaken for storage.
/// </summary>
public sealed class InMemoryAppDbContext(DbContextOptions<InMemoryAppDbContext> options) : AppDbContext(options);

internal sealed class UtcDateTimeConverter()
    : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

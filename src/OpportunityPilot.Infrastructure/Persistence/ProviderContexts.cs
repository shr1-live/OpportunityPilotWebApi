using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Domain.Campaigns;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Infrastructure.Persistence;

/// <summary>Local development against SQL Server LocalDB.</summary>
public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SourceItem>().HasIndex(i => new { i.SourceId, i.ExternalId }).HasFilter("[ExternalId] IS NOT NULL");
        // At most one queued-or-running job per campaign, so two quick clicks cannot start two runs.
        modelBuilder.Entity<ResearchJob>().HasIndex(j => j.CampaignId, "UX_research_jobs_active_campaign")
            .IsUnique().HasFilter("[State] IN (N'Queued', N'Running')");
    }

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
        modelBuilder.Entity<Campaign>(e =>
        {
            e.Property(c => c.CriteriaJson).HasColumnType("jsonb");
            e.Property(c => c.WeightsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ImportBatch>().Property(b => b.RowsJson).HasColumnType("jsonb");
        modelBuilder.Entity<ResearchJob>().Property(j => j.CountsJson).HasColumnType("jsonb");
        modelBuilder.Entity<Opportunity>(e =>
        {
            e.Property(o => o.BreakdownJson).HasColumnType("jsonb");
            e.Property(o => o.FactsJson).HasColumnType("jsonb");
            e.Property(o => o.GapsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<SourceItem>().HasIndex(i => new { i.SourceId, i.ExternalId }).HasFilter("\"ExternalId\" IS NOT NULL");
        // At most one queued-or-running job per campaign, so two quick clicks cannot start two runs.
        modelBuilder.Entity<ResearchJob>().HasIndex(j => j.CampaignId, "UX_research_jobs_active_campaign")
            .IsUnique().HasFilter("\"State\" IN ('Queued', 'Running')");
    }
}

/// <summary>
/// Demo mode: used when no connection string is configured. Nothing survives a restart, which on a free
/// host happens whenever the instance sleeps. Reported through capabilities so it is never mistaken for storage.
/// Unique and filtered indexes are not enforced here; the services check for duplicates themselves.
/// </summary>
public sealed class InMemoryAppDbContext(DbContextOptions<InMemoryAppDbContext> options) : AppDbContext(options);

internal sealed class UtcDateTimeConverter()
    : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

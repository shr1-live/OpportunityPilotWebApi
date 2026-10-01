using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Domain.Agents;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Profiles;

namespace OpportunityPilot.Infrastructure.Persistence;

/// <summary>
/// Provider-neutral model. Each provider has its own subclass so it gets a separate migration set
/// (SqlServer for local LocalDB, Postgres for Supabase). Tables live in the "app" schema, which
/// Supabase's Data API does not expose by default.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options), IAppDbContext
{
    public const string Schema = "app";

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<AgentKey> AgentKeys => Set<AgentKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Profile>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.StructuredDataJson).IsRequired();
            e.Property(p => p.Version).IsConcurrencyToken();
            e.HasIndex(p => new { p.OwnerId, p.UpdatedAt });
        });

        modelBuilder.Entity<JobApplication>(e =>
        {
            e.ToTable("job_applications");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.Platform).HasConversion<string>().HasMaxLength(32);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(a => a.ExternalJobId).HasMaxLength(100).IsRequired();
            e.Property(a => a.JobUrl).HasMaxLength(1000).IsRequired();
            e.Property(a => a.Title).HasMaxLength(300).IsRequired();
            e.Property(a => a.Company).HasMaxLength(300).IsRequired();
            e.Property(a => a.Location).HasMaxLength(200);
            e.Property(a => a.Detail).HasMaxLength(1000);
            e.HasIndex(a => new { a.OwnerId, a.Platform, a.ExternalJobId }).IsUnique();
            e.HasIndex(a => new { a.OwnerId, a.OccurredAt });
        });

        modelBuilder.Entity<AgentKey>(e =>
        {
            e.ToTable("agent_keys");
            e.HasKey(k => k.Id);
            e.Property(k => k.Id).ValueGeneratedNever();
            e.Property(k => k.Name).HasMaxLength(100).IsRequired();
            e.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
            e.Property(k => k.Prefix).HasMaxLength(12).IsRequired();
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasIndex(k => k.OwnerId);
        });
    }
}

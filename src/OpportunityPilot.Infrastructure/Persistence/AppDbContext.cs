using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
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
    }
}

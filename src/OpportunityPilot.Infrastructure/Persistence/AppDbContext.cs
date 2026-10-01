using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Domain.Agents;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Campaigns;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Domain.Research;

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
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceItem> SourceItems => Set<SourceItem>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ResearchJob> ResearchJobs => Set<ResearchJob>();
    public DbSet<ResearchEvent> ResearchEvents => Set<ResearchEvent>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityEvidence> OpportunityEvidence => Set<OpportunityEvidence>();
    public DbSet<Activity> Activities => Set<Activity>();

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

        modelBuilder.Entity<Campaign>(e =>
        {
            e.ToTable("campaigns");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Mode).HasConversion<string>().HasMaxLength(32);
            e.Property(c => c.Name).HasMaxLength(Campaign.MaxNameLength).IsRequired();
            e.Property(c => c.Goal).HasMaxLength(Campaign.MaxGoalLength).IsRequired();
            e.Property(c => c.CriteriaJson).IsRequired();
            e.Property(c => c.WeightsJson).IsRequired();
            e.Property(c => c.Version).IsConcurrencyToken();
            e.HasOne<Profile>().WithMany().HasForeignKey(c => c.ProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.OwnerId, c.CreatedAt });
        });

        modelBuilder.Entity<Source>(e =>
        {
            e.ToTable("sources");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.Platform).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.Label).HasMaxLength(Source.MaxLabelLength).IsRequired();
            e.Property(s => s.Url).HasMaxLength(Source.MaxUrlLength);
            e.Property(s => s.Text).HasMaxLength(Source.MaxTextLength);
            e.Property(s => s.PermissionNote).HasMaxLength(Source.MaxPermissionNoteLength);
            e.Property(s => s.SafeError).HasMaxLength(Source.MaxSafeErrorLength);
            e.HasOne<Campaign>().WithMany().HasForeignKey(s => s.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.OwnerId, s.CampaignId, s.CreatedAt });
        });

        modelBuilder.Entity<SourceItem>(e =>
        {
            e.ToTable("source_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.ExternalId).HasMaxLength(SourceItem.MaxExternalIdLength);
            e.Property(i => i.Title).HasMaxLength(SourceItem.MaxTitleLength).IsRequired();
            e.Property(i => i.Organization).HasMaxLength(SourceItem.MaxOrganizationLength).IsRequired();
            e.Property(i => i.Location).HasMaxLength(SourceItem.MaxLocationLength);
            e.Property(i => i.Url).HasMaxLength(SourceItem.MaxUrlLength);
            e.Property(i => i.Description).HasMaxLength(SourceItem.MaxDescriptionLength);
            e.Property(i => i.Website).HasMaxLength(SourceItem.MaxUrlLength);
            e.Property(i => i.Country).HasMaxLength(SourceItem.MaxCountryLength);
            e.Property(i => i.Industry).HasMaxLength(SourceItem.MaxIndustryLength);
            e.HasOne<Source>().WithMany().HasForeignKey(i => i.SourceId).OnDelete(DeleteBehavior.Cascade);
            // Providers add "ExternalId IS NOT NULL" (SqlServer does so by itself; Postgres in its subclass).
            e.HasIndex(i => new { i.SourceId, i.ExternalId }).IsUnique();
            e.HasIndex(i => new { i.SourceId, i.UpdatedAt });
        });

        modelBuilder.Entity<ImportBatch>(e =>
        {
            e.ToTable("import_batches");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).ValueGeneratedNever();
            e.Property(b => b.RowsJson).IsRequired();
            // Two simultaneous commits of one preview: the second update finds Committed already true and fails.
            e.Property(b => b.Committed).IsConcurrencyToken();
            e.HasOne<Campaign>().WithMany().HasForeignKey(b => b.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => new { b.OwnerId, b.CreatedAt });
        });

        modelBuilder.Entity<ResearchJob>(e =>
        {
            e.ToTable("research_jobs");
            e.HasKey(j => j.Id);
            e.Property(j => j.Id).ValueGeneratedNever();
            e.Property(j => j.State).HasConversion<string>().HasMaxLength(32);
            e.Property(j => j.Stage).HasConversion<string>().HasMaxLength(32);
            e.Property(j => j.CountsJson).IsRequired();
            e.Property(j => j.SafeError).HasMaxLength(ResearchJob.MaxSafeErrorLength);
            e.Property(j => j.Version).IsConcurrencyToken();
            e.HasOne<Campaign>().WithMany().HasForeignKey(j => j.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(j => new { j.OwnerId, j.CampaignId, j.CreatedAt });
            // The processor's poll: queued jobs and running jobs whose lease may have expired.
            e.HasIndex(j => new { j.State, j.CreatedAt });
        });

        modelBuilder.Entity<ResearchEvent>(e =>
        {
            e.ToTable("research_events");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Id).ValueGeneratedNever();
            e.Property(ev => ev.Stage).HasConversion<string>().HasMaxLength(32);
            e.Property(ev => ev.Level).HasConversion<string>().HasMaxLength(32);
            e.Property(ev => ev.Message).HasMaxLength(ResearchEvent.MaxMessageLength).IsRequired();
            e.HasOne<ResearchJob>().WithMany().HasForeignKey(ev => ev.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(ev => new { ev.JobId, ev.At });
        });

        modelBuilder.Entity<Evidence>(e =>
        {
            e.ToTable("evidence");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Id).ValueGeneratedNever();
            e.Property(ev => ev.Url).HasMaxLength(Domain.Research.Evidence.MaxUrlLength);
            e.Property(ev => ev.ContentHash).HasMaxLength(64).IsRequired();
            e.Property(ev => ev.Excerpt).HasMaxLength(Domain.Research.Evidence.MaxExcerptLength).IsRequired();
            e.Property(ev => ev.ExtractionMethod).HasMaxLength(32).IsRequired();
            e.HasOne<Campaign>().WithMany().HasForeignKey(ev => ev.CampaignId).OnDelete(DeleteBehavior.Cascade);
            // No FK to the source: deleting a source must not erase the evidence behind existing scores.
            e.HasIndex(ev => new { ev.CampaignId, ev.SourceId, ev.ContentHash });
            e.HasIndex(ev => ev.OwnerId);
        });

        modelBuilder.Entity<Opportunity>(e =>
        {
            e.ToTable("opportunities");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).ValueGeneratedNever();
            e.Property(o => o.Mode).HasConversion<string>().HasMaxLength(32);
            e.Property(o => o.Outcome).HasConversion<string>().HasMaxLength(32);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(o => o.Platform).HasConversion<string>().HasMaxLength(32);
            e.Property(o => o.DedupeKey).HasMaxLength(Opportunity.MaxDedupeKeyLength).IsRequired();
            e.Property(o => o.Title).HasMaxLength(Opportunity.MaxTitleLength).IsRequired();
            e.Property(o => o.Organization).HasMaxLength(Opportunity.MaxOrganizationLength).IsRequired();
            e.Property(o => o.Location).HasMaxLength(Opportunity.MaxLocationLength);
            e.Property(o => o.Url).HasMaxLength(Opportunity.MaxUrlLength);
            e.Property(o => o.ApplyUrl).HasMaxLength(Opportunity.MaxUrlLength);
            e.Property(o => o.ExternalId).HasMaxLength(Opportunity.MaxExternalIdLength);
            e.Property(o => o.Description).HasMaxLength(Opportunity.MaxDescriptionLength);
            e.Property(o => o.OutcomeReason).HasMaxLength(Opportunity.MaxOutcomeReasonLength);
            e.Property(o => o.BreakdownJson).IsRequired();
            e.Property(o => o.FactsJson).IsRequired();
            e.Property(o => o.GapsJson).IsRequired();
            e.Property(o => o.Version).IsConcurrencyToken();
            e.HasOne<Campaign>().WithMany().HasForeignKey(o => o.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(o => new { o.CampaignId, o.DedupeKey }).IsUnique();
            e.HasIndex(o => new { o.OwnerId, o.CampaignId, o.Score });
            e.HasIndex(o => new { o.OwnerId, o.Status, o.UpdatedAt });
        });

        modelBuilder.Entity<OpportunityEvidence>(e =>
        {
            e.ToTable("opportunity_evidence");
            e.HasKey(l => new { l.OpportunityId, l.EvidenceId });
            e.HasOne<Opportunity>().WithMany().HasForeignKey(l => l.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            // Restrict, not cascade: SQL Server rejects two cascade paths from campaigns (via opportunities and via evidence).
            e.HasOne<Evidence>().WithMany().HasForeignKey(l => l.EvidenceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.EvidenceId);
        });

        modelBuilder.Entity<Activity>(e =>
        {
            e.ToTable("activities");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.Kind).HasMaxLength(32).IsRequired();
            e.Property(a => a.Detail).HasMaxLength(Activity.MaxDetailLength).IsRequired();
            e.HasOne<Opportunity>().WithMany().HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.OpportunityId, a.OccurredAt });
            e.HasIndex(a => a.OwnerId);
        });
    }
}

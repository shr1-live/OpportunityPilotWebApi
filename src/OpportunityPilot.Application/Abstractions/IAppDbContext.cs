using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Domain.Agents;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Campaigns;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Abstractions;

/// <summary>Narrow persistence boundary. EF Core already supplies change tracking and the unit of work.</summary>
public interface IAppDbContext
{
    DbSet<Profile> Profiles { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<AgentKey> AgentKeys { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<Source> Sources { get; }
    DbSet<SourceItem> SourceItems { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ResearchJob> ResearchJobs { get; }
    DbSet<ResearchEvent> ResearchEvents { get; }
    DbSet<Evidence> Evidence { get; }
    DbSet<Opportunity> Opportunities { get; }
    DbSet<OpportunityEvidence> OpportunityEvidence { get; }
    DbSet<Activity> Activities { get; }

    /// <summary>The research runner clears tracked state before recording a failure, so a half-applied batch is not saved.</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

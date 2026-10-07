using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Domain.Agents;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Auth;
using OpportunityPilot.Domain.Automation;
using OpportunityPilot.Domain.Campaigns;
using OpportunityPilot.Domain.Drafts;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Outreach;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Domain.Research;
using OpportunityPilot.Domain.Sales;
using OpportunityPilot.Domain.Staffing;

namespace OpportunityPilot.Application.Abstractions;

/// <summary>Narrow persistence boundary. EF Core already supplies change tracking and the unit of work.</summary>
public interface IAppDbContext
{
    DbSet<Profile> Profiles { get; }
    DbSet<ProfileVersion> ProfileVersions { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<AgentKey> AgentKeys { get; }
    DbSet<GuestSession> GuestSessions { get; }
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
    DbSet<OutreachDraft> OutreachDrafts { get; }
    DbSet<Suppression> Suppressions { get; }
    DbSet<NextAction> NextActions { get; }
    DbSet<SalesProject> SalesProjects { get; }
    DbSet<SalesBid> SalesBids { get; }
    DbSet<CampaignSchedule> CampaignSchedules { get; }
    DbSet<StaffingAccount> StaffingAccounts { get; }
    DbSet<StaffingContact> StaffingContacts { get; }
    DbSet<StaffingDeal> StaffingDeals { get; }
    DbSet<StaffingDealActivity> StaffingDealActivities { get; }

    /// <summary>The research runner clears tracked state before recording a failure, so a half-applied batch is not saved.</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

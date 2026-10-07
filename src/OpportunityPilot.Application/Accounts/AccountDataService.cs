using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;

namespace OpportunityPilot.Application.Accounts;

public sealed record DeleteAccountDataRequest(bool Confirm);

public sealed class AccountDataService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<object> ExportAsync(CancellationToken ct)
    {
        var owner = user.OwnerId;
        return new
        {
            exportedAt = clock.GetUtcNow().UtcDateTime,
            ownerId = owner,
            profiles = await db.Profiles.Where(x => x.OwnerId == owner).ToListAsync(ct),
            campaigns = await db.Campaigns.Where(x => x.OwnerId == owner).ToListAsync(ct),
            sources = await db.Sources.Where(x => x.OwnerId == owner).ToListAsync(ct),
            opportunities = await db.Opportunities.Where(x => x.OwnerId == owner).ToListAsync(ct),
            activities = await db.Activities.Where(x => x.OwnerId == owner).ToListAsync(ct),
            drafts = await db.OutreachDrafts.Where(x => x.OwnerId == owner).ToListAsync(ct),
            nextActions = await db.NextActions.Where(x => x.OwnerId == owner).ToListAsync(ct),
            suppressions = await db.Suppressions.Where(x => x.OwnerId == owner).ToListAsync(ct),
            applications = await db.JobApplications.Where(x => x.OwnerId == owner).ToListAsync(ct),
            salesProjects = await db.SalesProjects.Where(x => x.OwnerId == owner).ToListAsync(ct),
            salesBids = await db.SalesBids.Where(x => x.OwnerId == owner).ToListAsync(ct),
            upworkOpportunities = await db.UpworkOpportunities.Where(x => x.OwnerId == owner).ToListAsync(ct),
            wellfoundJobs = await db.WellfoundJobs.Where(x => x.OwnerId == owner).ToListAsync(ct),
            wellfoundApplications = await db.WellfoundApplications.Where(x => x.OwnerId == owner).ToListAsync(ct),
            wellfoundActivities = await db.WellfoundActivities.Where(x => x.OwnerId == owner).ToListAsync(ct)
        };
    }

    public async Task DeleteAsync(DeleteAccountDataRequest request, CancellationToken ct)
    {
        if (request?.Confirm != true) throw new RequestValidationException(new Dictionary<string, string[]> { ["confirm"] = ["Set confirm to true to permanently delete account data."] });
        var owner = user.OwnerId;
        db.WellfoundActivities.RemoveRange(await db.WellfoundActivities.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.WellfoundApplications.RemoveRange(await db.WellfoundApplications.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.WellfoundJobs.RemoveRange(await db.WellfoundJobs.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.UpworkOpportunities.RemoveRange(await db.UpworkOpportunities.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.SalesProjects.RemoveRange(await db.SalesProjects.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.JobApplications.RemoveRange(await db.JobApplications.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.AgentKeys.RemoveRange(await db.AgentKeys.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.Suppressions.RemoveRange(await db.Suppressions.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.Campaigns.RemoveRange(await db.Campaigns.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.Profiles.RemoveRange(await db.Profiles.Where(x => x.OwnerId == owner).ToListAsync(ct));
        db.GuestSessions.RemoveRange(await db.GuestSessions.Where(x => x.OwnerId == owner).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}

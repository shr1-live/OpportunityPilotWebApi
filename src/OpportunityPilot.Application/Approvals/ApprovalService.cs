using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Approvals;

/// <summary>
/// The batch approval queue: research suggests (New → Suggested, campaign auto-suggest), the user approves
/// (→ Shortlisted) or rejects (→ Dismissed) many at once. Nothing is applied before approval: the agent shortlist only
/// offers Shortlisted opportunities.
/// </summary>
public sealed class ApprovalService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int DefaultTake = 100;
    public const int MaxTake = 200;
    public const int MaxDecisions = 200;
    private const int SaveTries = 3;

    private IQueryable<Opportunity> Suggested =>
        db.Opportunities.Where(o => o.OwnerId == user.OwnerId && o.Status == OpportunityStatus.Suggested);

    /// <summary>Suggested opportunities, highest score first; optionally one campaign (404 when it is not the caller's).</summary>
    public async Task<ApprovalPageDto> ListAsync(Guid? campaignId, int take, int skip, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);
        var query = Suggested;
        if (campaignId is { } id)
        {
            if (!await db.Campaigns.AnyAsync(c => c.Id == id && c.OwnerId == user.OwnerId, ct))
                throw new NotFoundException("Campaign not found.");
            query = query.Where(o => o.CampaignId == id);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.Score).ThenByDescending(o => o.Coverage).ThenByDescending(o => o.UpdatedAt).ThenBy(o => o.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        var campaignIds = items.Select(o => o.CampaignId).Distinct().ToList();
        var names = await db.Campaigns.Where(c => c.OwnerId == user.OwnerId && campaignIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return new ApprovalPageDto(total, items.Select(o => new ApprovalItem(
            o.Id, o.CampaignId, names.GetValueOrDefault(o.CampaignId, string.Empty), o.Title, o.Organization, o.Location, o.Platform,
            o.ApplyUrl ?? o.Url, o.Score, o.Coverage, o.OutcomeReason, AppliesViaFor(o.Platform))).ToList());
    }

    public async Task<int> CountAsync(CancellationToken ct) => await Suggested.CountAsync(ct);

    public async Task<DecideApprovalsResult> DecideAllAsync(DecideAllApprovalsRequest request, CancellationToken ct)
    {
        var query = Suggested;
        if (request.CampaignId is { } campaignId)
        {
            if (!await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.OwnerId == user.OwnerId, ct))
                throw new NotFoundException("Campaign not found.");
            query = query.Where(x => x.CampaignId == campaignId);
        }
        var rows = await query.ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var row in rows)
            if (row.DecideSuggestion(request.Approve, now) is { } activity) db.Activities.Add(activity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Some opportunities changed while the batch was being approved. Reload and try again."); }
        return request.Approve ? new(rows.Count, 0, 0) : new(0, rows.Count, 0);
    }

    /// <summary>
    /// Approves and rejects in one save. Ids that are not the caller's, unknown, or no longer Suggested are counted as
    /// skipped rather than failing the batch, so a stale screen never blocks the rest of the decisions.
    /// </summary>
    public async Task<DecideApprovalsResult> DecideAsync(DecideApprovalsRequest request, CancellationToken ct)
    {
        var (approve, reject) = Validate(request);
        var requested = approve.Count + reject.Count;

        for (var attempt = 1; ; attempt++)
        {
            var ids = approve.Concat(reject).ToList();
            var found = await Suggested.Where(o => ids.Contains(o.Id)).ToListAsync(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            int approved = 0, rejected = 0;
            foreach (var o in found)
            {
                var yes = approve.Contains(o.Id);
                if (o.DecideSuggestion(yes, now) is not { } activity) continue;
                db.Activities.Add(activity);
                if (yes) approved++;
                else rejected++;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return new DecideApprovalsResult(approved, rejected, requested - approved - rejected);
            }
            catch (DbUpdateConcurrencyException) when (attempt < SaveTries)
            {
                // Research re-scored (or the user moved) one of them in between: read again and re-apply the decisions.
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("Some of these opportunities are changing right now (for example by research). Try again.");
            }
        }
    }

    /// <summary>Supported signed-in platforms are applied to by the desktop agent; every other source through its apply URL by the user.</summary>
    public static AppliesVia AppliesViaFor(JobPlatform? platform) =>
        platform is JobPlatform.LinkedIn or JobPlatform.Naukri or JobPlatform.Instahyre ? AppliesVia.Agent : AppliesVia.You;

    /// <summary>Duplicate ids within one list count once; an empty id is skipped like any unknown id.</summary>
    public static (HashSet<Guid> Approve, HashSet<Guid> Reject) Validate(DecideApprovalsRequest? request)
    {
        if (request is null) throw new RequestValidationException(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });
        var approve = (request.Approve ?? []).ToHashSet();
        var reject = (request.Reject ?? []).ToHashSet();

        var errors = new Dictionary<string, string[]>();
        if (approve.Count + reject.Count == 0)
            errors["approve"] = ["Choose at least one opportunity to approve or reject."];
        else if (approve.Count + reject.Count > MaxDecisions)
            errors["approve"] = [$"At most {MaxDecisions} opportunities can be decided at once."];
        if (approve.Overlaps(reject))
            errors["reject"] = ["An opportunity cannot be approved and rejected at once."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
        return (approve, reject);
    }
}

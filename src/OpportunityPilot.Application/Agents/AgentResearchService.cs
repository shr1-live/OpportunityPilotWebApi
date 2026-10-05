using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Agents;

/// <summary>
/// The desktop agent as a research source and as the applier of shortlisted jobs. Called only with an agent key;
/// the owner is the key's owner. Only Job campaigns are visible to the agent.
/// </summary>
public sealed class AgentResearchService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int MaxItemsPerDelivery = 100;
    public const int MaxItemsPerSource = 1000;
    public const int MaxShortlist = 200;

    public async Task<IReadOnlyList<AgentCampaignDto>> CampaignsAsync(CancellationToken ct)
    {
        var campaigns = await db.Campaigns
            .Where(c => c.OwnerId == user.OwnerId && c.Mode == OpportunityMode.Job)
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToListAsync(ct);
        return campaigns.Select(c => new AgentCampaignDto(c.Id, c.Name, c.Mode, CampaignCriteria.FromJson(c.CriteriaJson))).ToList();
    }

    /// <summary>Upserts postings into the campaign's single Agent source for the platform, deduped by external id.</summary>
    public async Task<AgentPostingsResult> DeliverAsync(Guid campaignId, AgentPostingsRequest request, CancellationToken ct)
    {
        var ownerId = user.OwnerId;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Campaign not found.");
        if (campaign.Mode != OpportunityMode.Job)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["campaign"] = ["Only Job campaigns accept postings from the agent."] });
        var items = Validate(request);

        var now = clock.GetUtcNow().UtcDateTime;
        var platform = request.Platform;
        var source = await db.Sources.FirstOrDefaultAsync(
            s => s.OwnerId == ownerId && s.CampaignId == campaignId && s.Kind == SourceKind.Agent && s.Platform == platform, ct);
        if (source is null)
        {
            source = new Source(ownerId, campaignId, SourceKind.Agent, $"{platform} (desktop agent)", null, null,
                "Collected by the desktop agent from your own logged-in browser.", platform, now);
            db.Sources.Add(source);
        }

        // Last one wins when the same posting appears twice in a batch.
        var latest = new Dictionary<string, AgentPostingItem>(StringComparer.Ordinal);
        foreach (var item in items) latest[item.ExternalId.Trim()] = item;
        var ids = latest.Keys.ToList();
        var existing = await db.SourceItems.Where(i => i.SourceId == source.Id && i.ExternalId != null && ids.Contains(i.ExternalId))
            .ToDictionaryAsync(i => i.ExternalId!, ct);

        foreach (var (externalId, item) in latest)
        {
            if (existing.TryGetValue(externalId, out var row))
                row.Update(item.Title, item.Company, item.Location, item.Url, item.Description, null, null, null, now);
            else
                db.SourceItems.Add(new SourceItem(ownerId, source.Id, externalId, item.Title, item.Company, item.Location, item.Url,
                    item.Description, null, null, null, now));
        }

        // Keep the source bounded: the oldest deliveries beyond the cap are dropped.
        var stored = await db.SourceItems.CountAsync(i => i.SourceId == source.Id, ct) + latest.Count - existing.Count;
        if (stored > MaxItemsPerSource)
        {
            var drop = await db.SourceItems.Where(i => i.SourceId == source.Id && !ids.Contains(i.ExternalId!))
                .OrderBy(i => i.UpdatedAt).ThenBy(i => i.Id)
                .Take(stored - MaxItemsPerSource)
                .ToListAsync(ct);
            db.SourceItems.RemoveRange(drop);
            stored -= drop.Count;
        }
        source.MarkDelivered(stored, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Another delivery for the same postings was saved at the same time. Send it again.");
        }

        Guid? jobId = request.QueueResearch
            ? await ResearchService.QueueForOwnerAsync(db, ownerId, campaignId, now, ct)
            : null;
        return new AgentPostingsResult(latest.Count, source.Id, jobId);
    }

    /// <summary>
    /// Job opportunities the user shortlisted (or approved) that the agent has not applied to yet. Postings from the
    /// public job-board sources (Greenhouse, Lever, Adzuna) are never offered: the user applies to those via their URL.
    /// </summary>
    public async Task<IReadOnlyList<AgentShortlistItem>> ShortlistAsync(JobPlatform? platform, CancellationToken ct)
    {
        var ownerId = user.OwnerId;
        var query = db.Opportunities.Where(o => o.OwnerId == ownerId && o.Mode == OpportunityMode.Job &&
                                                o.Status == OpportunityStatus.Shortlisted && o.Platform != null && o.ExternalId != null &&
                                                o.Platform != JobPlatform.Greenhouse && o.Platform != JobPlatform.Lever &&
                                                o.Platform != JobPlatform.Adzuna);
        if (platform is { } p) query = query.Where(o => o.Platform == p);
        var shortlisted = await query.OrderByDescending(o => o.Score).ThenBy(o => o.Id).Take(MaxShortlist).ToListAsync(ct);

        // An application the agent already reported as Applied (e.g. before the opportunity id was known) also counts.
        var jobIds = shortlisted.Select(o => o.ExternalId!).Distinct().ToList();
        var applied = (await db.JobApplications
                .Where(a => a.OwnerId == ownerId && a.Status == ApplicationStatus.Applied && jobIds.Contains(a.ExternalJobId))
                .Select(a => new { a.Platform, a.ExternalJobId })
                .ToListAsync(ct))
            .Select(a => (a.Platform.ToString(), a.ExternalJobId))
            .ToHashSet();

        return shortlisted
            .Where(o => !applied.Contains((o.Platform!.Value.ToString(), o.ExternalId!)))
            .Where(o => (o.ApplyUrl ?? o.Url) is not null)
            .Select(o => new AgentShortlistItem(o.Id, o.CampaignId, o.Platform!.Value, o.ExternalId!, (o.ApplyUrl ?? o.Url)!, o.Title, o.Organization))
            .ToList();
    }

    private static IReadOnlyList<AgentPostingItem> Validate(AgentPostingsRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null) throw new RequestValidationException(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });
        if (request.Platform is not (JobPlatform.LinkedIn or JobPlatform.Naukri)) errors["platform"] = ["Platform must be LinkedIn or Naukri."];

        var items = request.Items;
        if (items is null || items.Count == 0) errors["items"] = ["At least one item is required."];
        else if (items.Count > MaxItemsPerDelivery) errors["items"] = [$"At most {MaxItemsPerDelivery} items can be sent at once."];
        else
        {
            for (var i = 0; i < items.Count; i++)
            {
                var key = $"items[{i}]";
                var item = items[i];
                if (item is null)
                {
                    errors[key] = ["Item is required."];
                    continue;
                }
                Required(errors, $"{key}.externalId", "External id", item.ExternalId, SourceItem.MaxExternalIdLength);
                Required(errors, $"{key}.title", "Title", item.Title, SourceItem.MaxTitleLength);
                Required(errors, $"{key}.company", "Company", item.Company, SourceItem.MaxOrganizationLength);
                Optional(errors, $"{key}.location", "Location", item.Location, SourceItem.MaxLocationLength);
                Optional(errors, $"{key}.description", "Description", item.Description, SourceItem.MaxDescriptionLength);
                var url = item.Url?.Trim();
                if (string.IsNullOrEmpty(url)) errors[$"{key}.url"] = ["URL is required."];
                else if (url.Length > SourceItem.MaxUrlLength) errors[$"{key}.url"] = [$"URL must be at most {SourceItem.MaxUrlLength} characters."];
                else if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    errors[$"{key}.url"] = ["URL must be an absolute http or https URL."];
            }
        }

        if (errors.Count > 0) throw new RequestValidationException(errors);
        return items!;
    }

    private static void Required(Dictionary<string, string[]> errors, string key, string label, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[key] = [$"{label} is required."];
        else if (value.Trim().Length > max) errors[key] = [$"{label} must be at most {max} characters."];
    }

    private static void Optional(Dictionary<string, string[]> errors, string key, string label, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max) errors[key] = [$"{label} must be at most {max} characters."];
    }
}

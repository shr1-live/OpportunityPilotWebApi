using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Applications;

namespace OpportunityPilot.Application.Applications;

public sealed class ApplicationService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int MaxItemsPerReport = 100;
    public const int MaxExternalJobIdLength = 100;
    public const int MaxJobUrlLength = 1000;
    public const int MaxTitleLength = 300;
    public const int MaxCompanyLength = 300;
    public const int MaxLocationLength = 200;
    public const int MaxDetailLength = 1000;
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    // Agent and server clocks drift; anything further ahead than this is a bug, not skew.
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);

    private IQueryable<JobApplication> Owned => db.JobApplications.Where(a => a.OwnerId == user.OwnerId);

    public async Task<ApplicationReportResult> ReportAsync(ApplicationReportRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var items = Validate(request, now);

        // Last one wins when the same job appears twice in a batch.
        var latest = new Dictionary<(ApplicationPlatform Platform, string JobId), ApplicationReportItem>();
        foreach (var item in items) latest[(item.Platform, item.ExternalJobId.Trim())] = item;

        var ownerId = user.OwnerId;
        var platforms = latest.Keys.Select(k => k.Platform).Distinct().ToList();
        var jobIds = latest.Keys.Select(k => k.JobId).Distinct().ToList();
        var existing = (await Owned
                .Where(a => platforms.Contains(a.Platform) && jobIds.Contains(a.ExternalJobId))
                .ToListAsync(ct))
            .ToDictionary(a => (a.Platform, a.ExternalJobId));

        foreach (var ((platform, jobId), item) in latest)
        {
            var occurredAt = AsUtc(item.OccurredAt);
            if (existing.TryGetValue((platform, jobId), out var row))
                row.ApplyReport(item.JobUrl, item.Title, item.Company, item.Location, item.Status, item.Detail, occurredAt, now);
            else
                db.JobApplications.Add(new JobApplication(ownerId, platform, jobId, item.JobUrl, item.Title, item.Company,
                    item.Location, item.Status, item.Detail, occurredAt, now));
        }

        // A shortlisted job the agent applied to moves to Applied. Ids the caller does not own are ignored, not
        // rejected: the application itself happened and must still be recorded.
        var appliedTo = latest.Values.Where(i => i.Status == ApplicationStatus.Applied && i.OpportunityId is not null)
            .GroupBy(i => i.OpportunityId!.Value).ToDictionary(g => g.Key, g => g.Last());
        if (appliedTo.Count > 0)
        {
            var ids = appliedTo.Keys.ToList();
            var opportunities = await db.Opportunities.Where(o => o.OwnerId == ownerId && ids.Contains(o.Id)).ToListAsync(ct);
            foreach (var opportunity in opportunities)
            {
                var item = appliedTo[opportunity.Id];
                var activity = opportunity.MarkApplied($"Applied on {item.Platform} by the desktop agent.", now);
                if (activity is not null) db.Activities.Add(activity);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent report inserted the same job between our read and our write, or research re-scored a
            // shortlisted opportunity at the same moment (version check).
            throw new ConflictException("Another report for the same job was saved at the same time. Send the report again.");
        }
        return new ApplicationReportResult(latest.Count);
    }

    public async Task<ApplicationPageDto> ListAsync(
        ApplicationStatus? status, ApplicationPlatform? platform, int take, int skip, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);

        var query = Owned;
        if (status is { } s) query = query.Where(a => a.Status == s);
        if (platform is { } p) query = query.Where(a => a.Platform == p);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id)
            .Skip(skip).Take(take)
            .Select(a => new ApplicationDto(a.Id, a.Platform, a.ExternalJobId, a.JobUrl, a.Title, a.Company,
                a.Location, a.Status, a.Detail, a.OccurredAt, a.UpdatedAt))
            .ToListAsync(ct);
        return new ApplicationPageDto(total, items);
    }

    public async Task<ApplicationSummaryDto> SummaryAsync(CancellationToken ct)
    {
        var weekAgo = clock.GetUtcNow().UtcDateTime.AddDays(-7);
        var counts = await Owned
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var appliedLast7Days = await Owned.CountAsync(a => a.Status == ApplicationStatus.Applied && a.OccurredAt >= weekAgo, ct);
        var lastActivityAt = await Owned.MaxAsync(a => (DateTime?)a.OccurredAt, ct);

        int Count(ApplicationStatus s) => counts.GetValueOrDefault(s);
        return new ApplicationSummaryDto(
            Count(ApplicationStatus.Applied), appliedLast7Days, Count(ApplicationStatus.NeedsManual),
            Count(ApplicationStatus.DryRun), Count(ApplicationStatus.Skipped), Count(ApplicationStatus.Failed),
            lastActivityAt);
    }

    private static IReadOnlyList<ApplicationReportItem> Validate(ApplicationReportRequest? request, DateTime now)
    {
        var errors = new Dictionary<string, string[]>();
        var items = request?.Items;
        if (items is null || items.Count == 0)
            errors["items"] = ["At least one item is required."];
        else if (items.Count > MaxItemsPerReport)
            errors["items"] = [$"At most {MaxItemsPerReport} items can be reported at once."];
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

                if (!Enum.IsDefined(item.Platform)) errors[$"{key}.platform"] = ["Unknown platform."];
                if (!Enum.IsDefined(item.Status)) errors[$"{key}.status"] = ["Unknown status."];
                Required(errors, $"{key}.externalJobId", "External job id", item.ExternalJobId, MaxExternalJobIdLength);
                Required(errors, $"{key}.title", "Title", item.Title, MaxTitleLength);
                Required(errors, $"{key}.company", "Company", item.Company, MaxCompanyLength);
                Optional(errors, $"{key}.location", "Location", item.Location, MaxLocationLength);
                Optional(errors, $"{key}.detail", "Detail", item.Detail, MaxDetailLength);

                var url = item.JobUrl?.Trim();
                if (string.IsNullOrEmpty(url))
                    errors[$"{key}.jobUrl"] = ["Job URL is required."];
                else if (url.Length > MaxJobUrlLength)
                    errors[$"{key}.jobUrl"] = [$"Job URL must be at most {MaxJobUrlLength} characters."];
                else if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    errors[$"{key}.jobUrl"] = ["Job URL must be an absolute http or https URL."];

                if (item.OccurredAt == default)
                    errors[$"{key}.occurredAt"] = ["Occurred-at time is required."];
                else if (AsUtc(item.OccurredAt) > now + MaxFutureSkew)
                    errors[$"{key}.occurredAt"] = ["Occurred-at time cannot be in the future."];
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

    // A timestamp without a zone is taken as UTC; one with an offset is converted. Postgres timestamptz accepts only UTC.
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

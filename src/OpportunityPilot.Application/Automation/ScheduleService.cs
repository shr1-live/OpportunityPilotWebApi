using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Domain.Automation;

namespace OpportunityPilot.Application.Automation;

public sealed class ScheduleService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<CampaignScheduleDto>> ListAsync(CancellationToken ct) =>
        await (from schedule in db.CampaignSchedules
               join campaign in db.Campaigns on schedule.CampaignId equals campaign.Id
               where schedule.OwnerId == user.OwnerId && campaign.OwnerId == user.OwnerId
               orderby schedule.NextRunAt, schedule.Id
               select new CampaignScheduleDto(schedule.Id, schedule.CampaignId, campaign.Name, schedule.TimeZone,
                   schedule.CadenceMinutes, schedule.NextRunAt, schedule.Paused, schedule.LastQueuedAt,
                   schedule.LastSafeError, schedule.Version, schedule.UpdatedAt)).ToListAsync(ct);

    public async Task<CampaignScheduleDto> UpsertAsync(Guid campaignId, UpsertCampaignScheduleRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("schedule", "Schedule is required.");
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Campaign not found.");
        Validate(request);
        var now = clock.GetUtcNow().UtcDateTime;
        var row = await db.CampaignSchedules.FirstOrDefaultAsync(s => s.CampaignId == campaignId && s.OwnerId == user.OwnerId, ct);
        try
        {
            if (row is null)
            {
                if (request.ExpectedVersion is not null) throw new ConflictException("Schedule does not exist. Reload before saving.");
                row = new CampaignSchedule(user.OwnerId, campaignId, request.TimeZone, request.CadenceMinutes,
                    request.NextRunAt.ToUniversalTime(), request.Paused, now);
                db.CampaignSchedules.Add(row);
            }
            else
            {
                if (request.ExpectedVersion != row.Version)
                    throw new ConflictException($"Schedule was changed elsewhere (now version {row.Version}). Reload before saving.");
                row.Update(request.TimeZone, request.CadenceMinutes, request.NextRunAt.ToUniversalTime(), request.Paused, now);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Schedule was changed elsewhere. Reload before saving."); }
        catch (DbUpdateException) { throw new ConflictException("This campaign already has a schedule. Reload before saving."); }
        return ToDto(row, campaign.Name);
    }

    /// <summary>Claims and queues due schedules. The active-job constraint makes retries idempotent after a crash.</summary>
    public async Task<int> QueueDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var dueIds = await db.CampaignSchedules
            .Where(s => !s.Paused && s.NextRunAt <= now && (s.LeaseUntil == null || s.LeaseUntil <= now))
            .OrderBy(s => s.NextRunAt).Select(s => s.Id).Take(20).ToListAsync(ct);
        var queued = 0;
        foreach (var id in dueIds)
        {
            db.ChangeTracker.Clear();
            var row = await db.CampaignSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (row is null || !row.CanClaim(now)) continue;
            try
            {
                row.Claim(now, Lease);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) { continue; }

            string? error = null;
            try
            {
                await ResearchService.QueueForOwnerAsync(db, row.OwnerId, row.CampaignId, now, ct);
                queued++;
            }
            catch (RequestValidationException) { error = "Add at least one source before the next scheduled run."; }
            catch (NotFoundException) { error = "Campaign is no longer available."; }

            row.Complete(now, error);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { /* another worker reclaimed after lease expiry; active-job guard prevents duplicates */ }
        }
        return queued;
    }

    private static void Validate(UpsertCampaignScheduleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.TimeZone)) errors["timeZone"] = ["Time zone is required."];
        else try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone); }
            catch (TimeZoneNotFoundException) { errors["timeZone"] = ["Use a valid IANA or system time-zone id."]; }
            catch (InvalidTimeZoneException) { errors["timeZone"] = ["The time zone is invalid."]; }
        if (request.CadenceMinutes is < CampaignSchedule.MinCadenceMinutes or > CampaignSchedule.MaxCadenceMinutes)
            errors["cadenceMinutes"] = [$"Cadence must be {CampaignSchedule.MinCadenceMinutes}–{CampaignSchedule.MaxCadenceMinutes} minutes."];
        if (request.NextRunAt.Kind == DateTimeKind.Unspecified) errors["nextRunAt"] = ["Next run must include a UTC offset."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    private static CampaignScheduleDto ToDto(CampaignSchedule row, string campaignName) =>
        new(row.Id, row.CampaignId, campaignName, row.TimeZone, row.CadenceMinutes, row.NextRunAt, row.Paused,
            row.LastQueuedAt, row.LastSafeError, row.Version, row.UpdatedAt);

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

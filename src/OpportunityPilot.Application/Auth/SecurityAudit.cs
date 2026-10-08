using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Domain.Auth;

namespace OpportunityPilot.Application.Auth;

public sealed record SecurityEventDto(Guid Id, SecurityEventType Type, string? Detail, DateTime OccurredAt);

/// <summary>Adds security events to the current unit of work (saved with the action) and lists the owner's own events.</summary>
public sealed class SecurityAudit(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public void Record(Guid ownerId, SecurityEventType type, string? detail = null) =>
        db.SecurityEvents.Add(new SecurityEvent(ownerId, type, detail, clock.GetUtcNow().UtcDateTime));

    public async Task<IReadOnlyList<SecurityEventDto>> ListAsync(int take, CancellationToken ct) =>
        await db.SecurityEvents.Where(e => e.OwnerId == user.OwnerId).OrderByDescending(e => e.OccurredAt).ThenBy(e => e.Id)
            .Take(Math.Clamp(take, 1, 200)).Select(e => new SecurityEventDto(e.Id, e.Type, e.Detail, e.OccurredAt)).ToListAsync(ct);
}

using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Ai;
using OpportunityPilot.Domain.Auth;
using OpportunityPilot.Domain.Research;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.UnitTests;

public class RetentionServiceTests
{
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Removes_only_what_has_passed_its_retention_and_keeps_business_records()
    {
        await using var db = new InMemoryAppDbContext(new DbContextOptionsBuilder<InMemoryAppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var owner = Guid.NewGuid();
        var job = Guid.NewGuid();
        db.ImportBatches.Add(new ImportBatch(owner, Guid.NewGuid(), "[]", Now.AddDays(-30)));   // expired preview
        db.ImportBatches.Add(new ImportBatch(owner, Guid.NewGuid(), "[]", Now));                // still valid
        db.ResearchEvents.Add(new ResearchEvent(job, Now.AddDays(-91), ResearchStage.Gather, EventLevel.Info, "old"));
        db.ResearchEvents.Add(new ResearchEvent(job, Now.AddDays(-10), ResearchStage.Gather, EventLevel.Info, "recent"));
        db.AiUsages.Add(new AiUsage(owner, "ParseGoal", AiOutcome.Succeeded, 1, 1, null, Now.AddDays(-100)));
        db.AiUsages.Add(new AiUsage(owner, "ParseGoal", AiOutcome.Succeeded, 1, 1, null, Now.AddDays(-1)));
        db.SecurityEvents.Add(new SecurityEvent(owner, SecurityEventType.AgentKeyCreated, null, Now.AddDays(-400)));
        db.SecurityEvents.Add(new SecurityEvent(owner, SecurityEventType.AgentKeyCreated, null, Now.AddDays(-5)));
        db.GuestSessions.Add(new GuestSession(owner, new string('a', 64), Now.AddDays(-40), Now.AddDays(-10)));
        db.GuestSessions.Add(new GuestSession(Guid.NewGuid(), new string('b', 64), Now, Now.AddDays(30)));
        await db.SaveChangesAsync();

        var result = await new RetentionService(db, new FixedClock(Now)).RunOnceAsync(default);

        Assert.Equal(new RetentionResult(1, 1, 1, 1, 1), result);
        Assert.Single(db.ImportBatches);
        Assert.Equal("recent", Assert.Single(db.ResearchEvents).Message);
        Assert.Single(db.AiUsages);
        Assert.Single(db.SecurityEvents);
        Assert.Single(db.GuestSessions);
    }
}

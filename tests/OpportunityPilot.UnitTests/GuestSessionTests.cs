using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Auth;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.UnitTests;

public class GuestSessionTests
{
    private static readonly DateTime T0 = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Opaque_session_survives_a_new_context_and_only_its_hash_is_stored()
    {
        var databaseName = Guid.NewGuid().ToString();
        var clock = new SettableTimeProvider(T0);
        string token;
        Guid ownerId;

        await using (var first = Context(databaseName))
        {
            var issued = await new GuestSessionService(first, clock).IssueAsync(default);
            token = issued.Token;
            Assert.StartsWith(GuestSessionService.TokenPrefix, token);
            Assert.DoesNotContain('.', token);

            var stored = await first.GuestSessions.SingleAsync();
            ownerId = stored.OwnerId;
            Assert.Equal(GuestSessionService.Hash(token), stored.TokenHash);
            Assert.DoesNotContain(token, stored.TokenHash);
        }

        await using var afterRestart = Context(databaseName);
        var sessions = new GuestSessionService(afterRestart, clock);
        Assert.Equal(ownerId, await sessions.ValidateAsync(token, default));
        Assert.Null(await sessions.ValidateAsync(token[..^1] + "x", default));

        clock.UtcNow = T0.AddDays(31);
        Assert.Null(await sessions.ValidateAsync(token, default));
    }

    private static InMemoryAppDbContext Context(string databaseName) =>
        new(new DbContextOptionsBuilder<InMemoryAppDbContext>().UseInMemoryDatabase(databaseName).Options);

    private sealed class SettableTimeProvider(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }
}

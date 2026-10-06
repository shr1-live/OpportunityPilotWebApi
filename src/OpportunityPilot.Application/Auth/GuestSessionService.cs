using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Domain.Auth;

namespace OpportunityPilot.Application.Auth;

public sealed record IssuedGuestSession(string Token, DateTime ExpiresAt);

/// <summary>Issues and validates high-entropy opaque guest credentials. Plaintext tokens are never persisted.</summary>
public sealed class GuestSessionService(IAppDbContext db, TimeProvider clock)
{
    public const string TokenPrefix = "opg_";
    private const int MaxPresentedTokenLength = 100;
    private const int ExpiredCleanupBatchSize = 100;
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<IssuedGuestSession> IssueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = await db.GuestSessions
            .Where(s => s.ExpiresAt <= now)
            .OrderBy(s => s.ExpiresAt)
            .Take(ExpiredCleanupBatchSize)
            .ToListAsync(ct);
        if (expired.Count > 0) db.GuestSessions.RemoveRange(expired);

        var token = TokenPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now + Lifetime;
        db.GuestSessions.Add(new GuestSession(Guid.NewGuid(), Hash(token), now, expiresAt));
        await db.SaveChangesAsync(ct);
        return new IssuedGuestSession(token, expiresAt);
    }

    /// <summary>Returns the session owner only for a well-formed, known and unexpired credential.</summary>
    public async Task<Guid?> ValidateAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxPresentedTokenLength
            || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return null;

        var hash = Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.GuestSessions
            .Where(s => s.TokenHash == hash && s.ExpiresAt > now)
            .Select(s => (Guid?)s.OwnerId)
            .SingleOrDefaultAsync(ct);
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

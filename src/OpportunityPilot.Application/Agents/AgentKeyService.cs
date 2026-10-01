using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Agents;

namespace OpportunityPilot.Application.Agents;

public sealed class AgentKeyService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const string KeyPrefix = "opk_";
    public const int PrefixLength = 12;
    public const int MaxNameLength = 100;
    public const int MaxActiveKeysPerOwner = 10;

    // "opk_" + 43 base64url characters; anything much longer is not one of ours and is rejected before hashing.
    private const int MaxPresentedKeyLength = 100;

    private IQueryable<AgentKey> Owned => db.AgentKeys.Where(k => k.OwnerId == user.OwnerId);

    public async Task<IReadOnlyList<AgentKeyDto>> ListAsync(CancellationToken ct) =>
        await Owned
            .Where(k => k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new AgentKeyDto(k.Id, k.Name, k.Prefix, k.CreatedAt, k.LastUsedAt))
            .ToListAsync(ct);

    public async Task<CreatedAgentKeyDto> CreateAsync(CreateAgentKeyRequest request, CancellationToken ct)
    {
        var name = request?.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
        if (name.Length > MaxNameLength)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["name"] = [$"Name must be at most {MaxNameLength} characters."] });
        if (await Owned.CountAsync(k => k.RevokedAt == null, ct) >= MaxActiveKeysPerOwner)
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"You already have {MaxActiveKeysPerOwner} active agent keys. Revoke one you no longer use first."]
            });

        var key = GenerateKey();
        var entity = new AgentKey(user.OwnerId, name, HashKey(key), PrefixOf(key), clock.GetUtcNow().UtcDateTime);
        db.AgentKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        return new CreatedAgentKeyDto(entity.Id, entity.Name, entity.Prefix, entity.CreatedAt, entity.LastUsedAt, key);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var key = await Owned.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException("Agent key not found.");
        key.Revoke(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Resolves a presented key to its owner, or null when it is unknown or revoked.
    /// Deliberately NOT owner-scoped: this is how the owner of an agent request is determined in the first place,
    /// so it must never be called with, or return data to, anyone but the authentication handler.
    /// </summary>
    public async Task<Guid?> ValidateAsync(string? presentedKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(presentedKey) || presentedKey.Length > MaxPresentedKeyLength
            || !presentedKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return null;

        var hash = HashKey(presentedKey);
        var key = await db.AgentKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null, ct);
        if (key is null) return null;

        key.MarkUsed(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return key.OwnerId;
    }

    /// <summary>"opk_" followed by 32 random bytes as unpadded base64url.</summary>
    public static string GenerateKey() => KeyPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Lowercase hex SHA-256. The key is high-entropy random, so a fast unsalted hash is sufficient.</summary>
    public static string HashKey(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string PrefixOf(string key) => key[..Math.Min(PrefixLength, key.Length)];
}

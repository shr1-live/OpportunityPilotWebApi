namespace OpportunityPilot.Application.Agents;

/// <summary>Never carries the key itself; <see cref="Prefix"/> is enough to tell keys apart.</summary>
public sealed record AgentKeyDto(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt);

public sealed record CreateAgentKeyRequest(string Name);

/// <summary>Returned once, at creation. The plaintext <see cref="Key"/> cannot be retrieved again.</summary>
public sealed record CreatedAgentKeyDto(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt, string Key);

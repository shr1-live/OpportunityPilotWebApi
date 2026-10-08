using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Ai;

public enum AiOutcome { Succeeded, Timeout, ProviderError, InvalidResponse, OverDailyLimit }

/// <summary>
/// One AI call attempt by an owner (including ones refused for budget). The daily limit counts these rows, so it holds
/// across restarts and instances. Never stores prompts, answers or keys — only what happened and how long it took.
/// </summary>
public sealed class AiUsage : IOwned
{
    public const int MaxOperationLength = 64;
    public const int MaxReasonLength = 200;

    private AiUsage() { }

    public AiUsage(Guid ownerId, string operation, AiOutcome outcome, int durationMs, int attempts, string? reason, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Operation = Guard.Required(operation, MaxOperationLength, nameof(operation));
        Outcome = outcome;
        DurationMs = Math.Max(0, durationMs);
        Attempts = Math.Max(0, attempts);
        Reason = Guard.TruncateOptional(reason, MaxReasonLength);
        OccurredAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public AiOutcome Outcome { get; private set; }
    public int DurationMs { get; private set; }
    public int Attempts { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAt { get; private set; }

    /// <summary>Calls that reached the provider count against the daily limit; refused ones do not.</summary>
    public bool Counts => Outcome != AiOutcome.OverDailyLimit;
}

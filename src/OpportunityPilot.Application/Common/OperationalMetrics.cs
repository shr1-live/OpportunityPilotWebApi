using System.Collections.Concurrent;

namespace OpportunityPilot.Application.Common;

public sealed record OperationStats(string Operation, long Count, long Failures, double AverageMs, double MaxMs, DateTime? LastFailureAt, string? LastFailure);

/// <summary>
/// In-process counters for research runs and provider calls since the API started (a free host restarts often, so these
/// describe the current instance only). Thread-safe; holds no owner data and no request content.
/// </summary>
public sealed class OperationalMetrics(TimeProvider clock)
{
    private sealed class Counter
    {
        public long Count, Failures;
        public double TotalMs, MaxMs;
        public DateTime? LastFailureAt;
        public string? LastFailure;
    }

    private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);

    public DateTime StartedAt { get; } = clock.GetUtcNow().UtcDateTime;

    /// <param name="failure">A safe, short reason (never a URL, key or message body); null when the call succeeded.</param>
    public void Record(string operation, TimeSpan duration, string? failure = null)
    {
        var c = _counters.GetOrAdd(operation, _ => new Counter());
        lock (c)
        {
            c.Count++;
            c.TotalMs += duration.TotalMilliseconds;
            c.MaxMs = Math.Max(c.MaxMs, duration.TotalMilliseconds);
            if (failure is null) return;
            c.Failures++;
            c.LastFailureAt = clock.GetUtcNow().UtcDateTime;
            c.LastFailure = failure.Length > 200 ? failure[..200] : failure;
        }
    }

    public IReadOnlyList<OperationStats> Snapshot() =>
        _counters.OrderBy(kv => kv.Key).Select(kv =>
        {
            lock (kv.Value)
            {
                var c = kv.Value;
                return new OperationStats(kv.Key, c.Count, c.Failures, c.Count == 0 ? 0 : Math.Round(c.TotalMs / c.Count, 1), Math.Round(c.MaxMs, 1),
                    c.LastFailureAt, c.LastFailure);
            }
        }).ToList();
}

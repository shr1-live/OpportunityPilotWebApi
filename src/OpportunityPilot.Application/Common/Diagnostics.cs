namespace OpportunityPilot.Application.Common;

/// <summary>Whether Supabase's public roles can use schema <c>app</c>. Checked is false when the database is not Postgres.</summary>
public sealed record SchemaExposure(bool Checked, IReadOnlyList<string> ExposedTo, string? Note);

public sealed record DatabaseDiagnostics(string Provider, bool Configured, bool Reachable, int? PendingMigrations, SchemaExposure SchemaExposure);

/// <summary>Deployment check without secrets, hosts, keys or owner data.</summary>
public sealed record DiagnosticsDto(
    string Version, string Environment, DateTime StartedAt, DateTime Now, DatabaseDiagnostics Database,
    IReadOnlyList<OperationStats> Operations);

public interface IDatabaseDiagnostics
{
    Task<DatabaseDiagnostics> CheckAsync(CancellationToken ct);
}

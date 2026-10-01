namespace OpportunityPilot.Application.Research;

/// <summary>
/// Claims and runs at most one research job. The hosted processor calls this in a loop; tests call it directly
/// (with the processor disabled) so runs are deterministic.
/// </summary>
public interface IResearchRunner
{
    /// <returns>True when a job was claimed (whatever its outcome); false when nothing was waiting.</returns>
    Task<bool> RunNextAsync(CancellationToken ct);
}

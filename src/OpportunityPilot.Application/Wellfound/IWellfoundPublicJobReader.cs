namespace OpportunityPilot.Application.Wellfound;

public sealed record WellfoundPublicJob(
    string ProviderJobId,
    string Title,
    string CompanyName,
    string ApplyUrl,
    string? Location,
    string? RemoteType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    decimal? EquityMin,
    decimal? EquityMax,
    DateTime? PostedAt,
    string EvidenceJson);

public sealed record WellfoundPublicReadResult(
    bool Ok,
    IReadOnlyList<WellfoundPublicJob> Jobs,
    string? FailureReason,
    DateTime ObservedAt);

public interface IWellfoundPublicJobReader
{
    Task<WellfoundPublicReadResult> ReadAsync(CancellationToken ct);
}

using OpportunityPilot.Domain.Wellfound;

namespace OpportunityPilot.Application.Wellfound;

public sealed record WellfoundStatusDto(
    string Mode, bool RecruitConnected, bool ReachConnected, string RecruitServer, string ReachServer,
    IReadOnlyList<string> RecruitReadScopes, IReadOnlyList<string> ReachReadScopes, string Detail);

public sealed record WellfoundJobDto(
    Guid Id, string ProviderJobId, WellfoundJobScope Scope, string Title, string CompanyName, string? Location,
    string? RemoteType, decimal? SalaryMin, decimal? SalaryMax, string? Currency, decimal? EquityMin,
    decimal? EquityMax, string? ExperienceLevel, string? EmploymentType, string? Industry, string? FundingStage,
    string? EmployeeCount, bool? VisaSponsorship, DateTime? PostedAt, string ApplyUrl, string? Summary,
    IReadOnlyList<string> Skills, int? MatchScore, WellfoundJobState State, bool IsDemo, int Version, DateTime UpdatedAt);

public sealed record WellfoundApplicationDto(
    Guid Id, Guid JobId, string JobTitle, string ProviderApplicationId, string CandidateName, int? FitScore,
    WellfoundApplicationState State, bool IsDemo, int Version, DateTime UpdatedAt);

public sealed record WellfoundActivityDto(
    Guid Id, WellfoundActivityKind Kind, string Detail, bool ProviderConfirmed, DateTime OccurredAt);

public sealed record WellfoundKpisDto(
    string Workspace, int Jobs, int Saved, int Applied, int Interviewing, int Offered,
    int Applicants, int Reviewing, int Shortlisted, int Rejected, int Activities, int DiscoveryJobs);

public sealed record ChangeWellfoundJobStateRequest(WellfoundJobState State, int ExpectedVersion);
public sealed record ChangeWellfoundApplicationStateRequest(WellfoundApplicationState State, int ExpectedVersion, bool Confirmed);
public sealed record LoadWellfoundDemoResult(int JobsAdded, int ApplicationsAdded);
public sealed record SyncWellfoundPublicResult(int Observed, int Added, int Updated, int DemoRowsRemoved, DateTime ObservedAt);

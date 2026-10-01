using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Applications;

/// <summary>
/// One job the local agent acted on, keyed by (owner, platform, external job id).
/// Repeated reports for the same job update this row instead of adding new ones.
/// </summary>
public class JobApplication : IOwned
{
    private JobApplication() { }

    public JobApplication(
        Guid ownerId, ApplicationPlatform platform, string externalJobId,
        string jobUrl, string title, string company, string? location,
        ApplicationStatus status, string? detail, DateTime occurredAt, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(externalJobId)) throw new ArgumentException("External job id is required.", nameof(externalJobId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Platform = platform;
        ExternalJobId = externalJobId.Trim();
        CreatedAt = utcNow;
        Status = status;
        Detail = Clean(detail);
        OccurredAt = occurredAt;
        Describe(jobUrl, title, company, location);
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public ApplicationPlatform Platform { get; private set; }
    public string ExternalJobId { get; private set; } = string.Empty;
    public string JobUrl { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Company { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? Detail { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Applies a later report for the same job. A submitted application cannot be undone, so once Applied
    /// the status, its detail and its time are kept; a later dry run, skip or failure only refreshes the job details.
    /// </summary>
    public void ApplyReport(
        string jobUrl, string title, string company, string? location,
        ApplicationStatus status, string? detail, DateTime occurredAt, DateTime utcNow)
    {
        Describe(jobUrl, title, company, location);
        if (Status != ApplicationStatus.Applied || status == ApplicationStatus.Applied)
        {
            Status = status;
            Detail = Clean(detail);
            OccurredAt = occurredAt;
        }
        UpdatedAt = utcNow;
    }

    private void Describe(string jobUrl, string title, string company, string? location)
    {
        if (string.IsNullOrWhiteSpace(jobUrl)) throw new ArgumentException("Job URL is required.", nameof(jobUrl));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(company)) throw new ArgumentException("Company is required.", nameof(company));
        JobUrl = jobUrl.Trim();
        Title = title.Trim();
        Company = company.Trim();
        Location = Clean(location);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

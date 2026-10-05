namespace OpportunityPilot.Domain.Opportunities;

/// <summary>
/// Where a job posting came from. Only LinkedIn and Naukri are applied to by the desktop agent; postings from the
/// public job-board sources (Greenhouse, Lever, Adzuna) are applied to by the user through their apply URL.
/// </summary>
public enum JobPlatform
{
    LinkedIn,
    Naukri,
    Other,
    Greenhouse,
    Lever,
    Adzuna
}

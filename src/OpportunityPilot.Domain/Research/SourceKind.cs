namespace OpportunityPilot.Domain.Research;

public enum SourceKind
{
    Paste,
    Csv,
    Url,
    Feed,
    Agent,

    /// <summary>A company's public Greenhouse job board; <see cref="Source.Url"/> holds the board token.</summary>
    Greenhouse,

    /// <summary>A company's public Lever postings; <see cref="Source.Url"/> holds the company slug.</summary>
    Lever,

    /// <summary>Adzuna job search (server keys) driven by the campaign's keywords and first location.</summary>
    Adzuna
}

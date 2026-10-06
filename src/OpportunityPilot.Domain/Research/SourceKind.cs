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
    Adzuna,

    /// <summary>A company's public Ashby job board; <see cref="Source.Url"/> holds the organization slug.</summary>
    Ashby,

    /// <summary>A company's public SmartRecruiters postings; <see cref="Source.Url"/> holds the company identifier.</summary>
    SmartRecruiters,

    /// <summary>A company's public Recruitee offers; <see cref="Source.Url"/> holds the account slug.</summary>
    Recruitee,

    /// <summary>A company's public Workable widget; <see cref="Source.Url"/> holds the account slug.</summary>
    Workable,

    /// <summary>The public, board-wide Remotive remote-jobs feed.</summary>
    Remotive,

    /// <summary>The public, board-wide Remote OK jobs feed.</summary>
    RemoteOk
}

namespace OpportunityPilot.Application.Imports;

public sealed record ImportPreviewRequest(Guid CampaignId, string Csv);

/// <param name="Row">Spreadsheet-style line number: the header is row 1, so the first data row is 2.</param>
/// <param name="Values">Recognised columns only, keyed by lowercase column name.</param>
public sealed record ImportRowDto(int Row, IReadOnlyDictionary<string, string> Values, IReadOnlyList<string> Errors);

/// <param name="Rows">The first 50 rows; the counts cover the whole file.</param>
public sealed record ImportPreviewDto(
    Guid ImportId,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ImportRowDto> Rows,
    int ValidCount,
    int ErrorCount);

public sealed record CommitImportRequest(string? Label);

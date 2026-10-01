using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Sources;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Imports;

/// <summary>CSV import in two steps: a preview with row-level errors, then a commit of the valid rows into a Csv source.</summary>
public sealed class ImportService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int MaxCsvBytes = 1024 * 1024;
    public const int MaxRows = 1000;
    public const int MaxColumns = 50;
    public const int PreviewRows = 50;

    private sealed record Column(string Name, bool Required, int MaxLength);

    private static readonly Column[] JobColumns =
    [
        new("title", true, SourceItem.MaxTitleLength),
        new("company", true, SourceItem.MaxOrganizationLength),
        new("location", false, SourceItem.MaxLocationLength),
        new("url", false, SourceItem.MaxUrlLength),
        new("description", false, SourceItem.MaxDescriptionLength),
        new("id", false, SourceItem.MaxExternalIdLength)
    ];

    private static readonly Column[] CustomerColumns =
    [
        new("name", true, SourceItem.MaxTitleLength),
        new("website", false, SourceItem.MaxUrlLength),
        new("country", false, SourceItem.MaxCountryLength),
        new("industry", false, SourceItem.MaxIndustryLength),
        new("description", false, SourceItem.MaxDescriptionLength)
    ];

    public async Task<ImportPreviewDto> PreviewAsync(ImportPreviewRequest request, CancellationToken ct)
    {
        if (request is null) throw new RequestValidationException(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == request.CampaignId && c.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Campaign not found.");

        var text = request.Csv ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) throw Invalid("CSV text is required.");
        if (Encoding.UTF8.GetByteCount(text) > MaxCsvBytes) throw Invalid("CSV must be at most 1 MB.");
        if (!Csv.TryParse(text, out var records, out var parseError)) throw Invalid(parseError!);
        if (records.Count == 0) throw Invalid("CSV is empty.");
        if (records.Count - 1 > MaxRows) throw Invalid($"CSV can have at most {MaxRows} data rows.");

        var header = records[0].Select(h => h.Trim()).ToArray();
        if (header.Length > MaxColumns) throw Invalid($"CSV can have at most {MaxColumns} columns.");

        var spec = campaign.Mode == OpportunityMode.Job ? JobColumns : CustomerColumns;
        var warnings = new List<string>();
        var index = new Dictionary<string, int>();
        for (var c = 0; c < header.Length; c++)
        {
            var name = header[c].ToLowerInvariant();
            var known = spec.FirstOrDefault(s => s.Name == name);
            if (known is null)
                warnings.Add(header[c].Length == 0 ? $"Column {c + 1} has no header and is ignored." : $"Unknown column \"{header[c]}\" is ignored.");
            else if (!index.TryAdd(known.Name, c))
                warnings.Add($"Column \"{header[c]}\" appears more than once; only the first is used.");
        }
        var missingRequired = spec.Where(s => s.Required && !index.ContainsKey(s.Name)).Select(s => s.Name).ToList();
        foreach (var name in missingRequired)
            warnings.Add($"Required column \"{name}\" is missing, so every row is invalid.");
        if (records.Count == 1) warnings.Add("The file has a header row but no data rows.");

        var rows = new List<ImportRowDto>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 1; r < records.Count; r++)
        {
            var record = records[r];
            var values = new Dictionary<string, string>();
            var errors = new List<string>();
            foreach (var name in missingRequired) errors.Add($"{name} is required (column missing).");
            foreach (var column in spec)
            {
                if (!index.TryGetValue(column.Name, out var at)) continue;
                var value = at < record.Length ? record[at].Trim() : string.Empty;
                if (value.Length == 0)
                {
                    if (column.Required) errors.Add($"{column.Name} is required.");
                    continue;
                }
                if (value.Length > column.MaxLength)
                {
                    errors.Add($"{column.Name} must be at most {column.MaxLength} characters.");
                    continue;
                }
                values[column.Name] = value;
            }
            if (record.Length > header.Length && record.Skip(header.Length).Any(v => v.Trim().Length > 0))
                errors.Add($"Row has {record.Length} values but the header has {header.Length} columns.");

            if (values.TryGetValue("url", out var url) && !IsHttpUrl(url))
                errors.Add("url must be an absolute http or https URL.");
            if (values.TryGetValue("website", out var website))
            {
                if (NormaliseWebsite(website) is { } normalised) values["website"] = normalised;
                else errors.Add("website must be a domain or an http(s) URL.");
            }
            if (values.TryGetValue("id", out var id) && !seenIds.Add(id))
                errors.Add($"id \"{id}\" appears more than once in this file.");

            rows.Add(new ImportRowDto(r + 1, values, errors));
        }

        var batch = new ImportBatch(user.OwnerId, campaign.Id, JsonSerializer.Serialize(rows, JsonSerializerOptions.Web),
            clock.GetUtcNow().UtcDateTime);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        var errorCount = rows.Count(r => r.Errors.Count > 0);
        return new ImportPreviewDto(batch.Id, header, warnings, rows.Take(PreviewRows).ToList(), rows.Count - errorCount, errorCount);
    }

    public async Task<SourceDto> CommitAsync(Guid importId, CommitImportRequest? request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var batch = await db.ImportBatches.FirstOrDefaultAsync(b => b.Id == importId && b.OwnerId == user.OwnerId, ct);
        if (batch is null || batch.IsExpired(now)) throw new NotFoundException("Import preview not found or expired. Preview the file again.");
        if (batch.Committed) throw new ConflictException("This import was already committed.");

        var label = request?.Label?.Trim();
        if (label is { Length: > Source.MaxLabelLength })
            throw new RequestValidationException(new Dictionary<string, string[]> { ["label"] = [$"Label must be at most {Source.MaxLabelLength} characters."] });

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == batch.CampaignId && c.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Campaign not found.");
        if (await db.Sources.CountAsync(s => s.OwnerId == user.OwnerId && s.CampaignId == campaign.Id, ct) >= SourceService.MaxSourcesPerCampaign)
            throw Invalid($"A campaign can have at most {SourceService.MaxSourcesPerCampaign} sources. Delete one you no longer use first.", "label");

        var rows = (JsonSerializer.Deserialize<List<ImportRowDto>>(batch.RowsJson, JsonSerializerOptions.Web) ?? [])
            .Where(r => r.Errors.Count == 0).ToList();
        if (rows.Count == 0) throw Invalid("The preview has no valid rows to import.", "importId");

        var source = new Source(user.OwnerId, campaign.Id, SourceKind.Csv,
            string.IsNullOrEmpty(label) ? $"CSV import {now:yyyy-MM-dd HH:mm}" : label, null, null, null, null, now);
        foreach (var row in rows)
        {
            string? V(string key) => row.Values.TryGetValue(key, out var v) ? v : null;
            var item = campaign.Mode == OpportunityMode.Job
                ? new SourceItem(user.OwnerId, source.Id, V("id"), V("title")!, V("company"), V("location"), V("url"),
                    V("description"), null, null, null, now)
                : new SourceItem(user.OwnerId, source.Id, null, V("name")!, V("name"), null, null,
                    V("description"), V("website"), V("country"), V("industry"), now);
            db.SourceItems.Add(item);
        }
        source.SetItemCount(rows.Count);
        db.Sources.Add(source);
        batch.Commit(now);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This import was committed at the same time elsewhere.");
        }
        return SourceService.ToDto(source);
    }

    private static RequestValidationException Invalid(string message, string key = "csv") =>
        new(new Dictionary<string, string[]> { [key] = [message] });

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>"acme.com" becomes "https://acme.com"; anything that is not a plausible http(s) site is rejected.</summary>
    public static string? NormaliseWebsite(string value)
    {
        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal)) candidate = "https://" + candidate;
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && uri.Host.Contains('.') && candidate.Length <= SourceItem.MaxUrlLength
            ? candidate
            : null;
    }
}

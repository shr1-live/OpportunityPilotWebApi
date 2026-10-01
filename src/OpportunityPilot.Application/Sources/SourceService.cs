using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Sources;

public sealed class SourceService(IAppDbContext db, ICurrentUser user, TimeProvider clock, IWebFetcher fetcher)
{
    public const int MaxSourcesPerCampaign = 20;

    public async Task<IReadOnlyList<SourceDto>> ListAsync(Guid campaignId, CancellationToken ct)
    {
        await EnsureCampaignAsync(campaignId, ct);
        var sources = await db.Sources
            .Where(s => s.OwnerId == user.OwnerId && s.CampaignId == campaignId)
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .ToListAsync(ct);
        return sources.Select(ToDto).ToList();
    }

    public async Task<SourceDto> CreateAsync(Guid campaignId, CreateSourceRequest request, CancellationToken ct)
    {
        await EnsureCampaignAsync(campaignId, ct);
        if (request is null) throw new RequestValidationException(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });

        var errors = new Dictionary<string, string[]>();
        string? url = null, text = null;
        switch (request.Kind)
        {
            case SourceKind.Paste:
                text = request.Text?.Trim();
                if (string.IsNullOrEmpty(text)) errors["text"] = ["Paste at least one posting or company."];
                else if (text.Length > Source.MaxTextLength) errors["text"] = [$"Pasted text must be at most {Source.MaxTextLength.ToString("N0", CultureInfo.InvariantCulture)} characters."];
                else if (PasteParser.Parse(text).Count == 0) errors["text"] = ["No postings or companies were found in the pasted text."];
                break;
            case SourceKind.Url or SourceKind.Feed:
                url = request.Url?.Trim();
                if (string.IsNullOrEmpty(url)) errors["url"] = ["URL is required."];
                else if (url.Length > Source.MaxUrlLength) errors["url"] = [$"URL must be at most {Source.MaxUrlLength} characters."];
                else if (fetcher.CheckUrl(url) is { } reason) errors["url"] = [reason];
                break;
            case SourceKind.Csv:
                errors["kind"] = ["CSV sources are created by committing an import preview (POST /api/v1/imports/preview)."];
                break;
            case SourceKind.Agent:
                errors["kind"] = ["Agent sources are created by the desktop agent."];
                break;
            default:
                errors["kind"] = ["Unknown source kind."];
                break;
        }

        var label = request.Label?.Trim();
        if (label is { Length: > Source.MaxLabelLength }) errors["label"] = [$"Label must be at most {Source.MaxLabelLength} characters."];
        var note = request.PermissionNote?.Trim();
        if (note is { Length: > Source.MaxPermissionNoteLength })
            errors["permissionNote"] = [$"Permission note must be at most {Source.MaxPermissionNoteLength} characters."];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        if (await db.Sources.CountAsync(s => s.OwnerId == user.OwnerId && s.CampaignId == campaignId, ct) >= MaxSourcesPerCampaign)
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["kind"] = [$"A campaign can have at most {MaxSourcesPerCampaign} sources. Delete one you no longer use first."]
            });

        var source = new Source(user.OwnerId, campaignId, request.Kind, string.IsNullOrEmpty(label) ? DefaultLabel(request.Kind, url) : label,
            url, text, note, platform: null, clock.GetUtcNow().UtcDateTime);
        if (request.Kind == SourceKind.Paste) source.SetItemCount(PasteParser.Parse(text).Count);
        db.Sources.Add(source);
        await db.SaveChangesAsync(ct);
        return ToDto(source);
    }

    /// <summary>Removes the source and its rows. Evidence already gathered from it stays, so existing scores remain traceable.</summary>
    public async Task DeleteAsync(Guid campaignId, Guid sourceId, CancellationToken ct)
    {
        await EnsureCampaignAsync(campaignId, ct);
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId && s.CampaignId == campaignId && s.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Source not found.");
        // Explicit, because InMemory (demo mode) does not run database cascades.
        db.SourceItems.RemoveRange(await db.SourceItems.Where(i => i.SourceId == source.Id).ToListAsync(ct));
        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
    }

    public static SourceDto ToDto(Source s) => new(
        s.Id, s.CampaignId, s.Kind, s.Label, s.Url, s.Platform, s.PermissionNote, s.Status, s.LastFetchedAt, s.SafeError,
        s.ItemCount, s.Text?.Length ?? 0, s.CreatedAt);

    private async Task EnsureCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        if (!await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.OwnerId == user.OwnerId, ct))
            throw new NotFoundException("Campaign not found.");
    }

    private static string DefaultLabel(SourceKind kind, string? url)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
        return kind switch
        {
            SourceKind.Paste => "Pasted text",
            SourceKind.Url => host ?? "Web page",
            SourceKind.Feed => host is null ? "Feed" : $"{host} feed",
            _ => kind.ToString()
        };
    }
}

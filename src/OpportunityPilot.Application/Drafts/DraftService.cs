using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Drafts;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Drafts;

public sealed class DraftService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<DraftDto> CreateAsync(Guid opportunityId, CreateDraftRequest request, CancellationToken ct)
    {
        if (request is null || !Enum.IsDefined(request.Channel))
            throw Invalid("channel", "Unknown draft channel.");
        if (request.Channel != DraftChannel.CoverNote)
            throw Invalid("channel", "Only CoverNote drafts are available in this milestone.");

        var ownerId = user.OwnerId;
        var opportunity = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId && o.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Opportunity not found.");
        if (opportunity.Mode != OpportunityMode.Job)
            throw Invalid("channel", "Cover notes can only be created for Job opportunities.");
        if (await db.OutreachDrafts.AnyAsync(d => d.OwnerId == ownerId && d.OpportunityId == opportunityId && d.Channel == request.Channel, ct))
            throw new ConflictException("This opportunity already has a cover note. Edit the existing draft instead.");

        var campaign = await db.Campaigns.FirstAsync(c => c.Id == opportunity.CampaignId && c.OwnerId == ownerId, ct);
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == campaign.ProfileId && p.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Campaign profile not found.");
        if (profile.ConfirmedAt is null)
            throw Invalid("profile", "Confirm the campaign profile before generating a cover note.");

        var generated = GenerateCoverNote(opportunity, campaign.CriteriaJson, profile.StructuredDataJson);
        var now = clock.GetUtcNow().UtcDateTime;
        var draft = new OutreachDraft(ownerId, opportunity.Id, request.Channel, request.Recipient, false, null,
            generated.Body, DraftSource.Template, JsonSerializer.Serialize(generated.Claims, Json), now);
        db.OutreachDrafts.Add(draft);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("This opportunity already has a cover note. Reload to see it.");
        }
        return ToDto(draft);
    }

    public async Task<IReadOnlyList<DraftDto>> ListForOpportunityAsync(Guid opportunityId, CancellationToken ct)
    {
        await EnsureOpportunityAsync(opportunityId, ct);
        var rows = await Owned.Where(d => d.OpportunityId == opportunityId)
            .OrderByDescending(d => d.UpdatedAt).ThenBy(d => d.Id).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DraftDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await FindAsync(id, ct));

    public async Task<DraftDto> UpdateAsync(Guid id, UpdateDraftRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw Invalid("body", "Draft body is required.");
        if (request.Body.Length > OutreachDraft.MaxBodyLength)
            throw Invalid("body", $"Draft body must be at most {OutreachDraft.MaxBodyLength} characters.");
        if (request.Recipient?.Trim().Length > OutreachDraft.MaxRecipientLength)
            throw Invalid("recipient", $"Recipient must be at most {OutreachDraft.MaxRecipientLength} characters.");
        if (request.Subject?.Trim().Length > OutreachDraft.MaxSubjectLength)
            throw Invalid("subject", $"Subject must be at most {OutreachDraft.MaxSubjectLength} characters.");

        var draft = await FindAsync(id, ct);
        if (draft.Version != request.ExpectedVersion)
            throw new ConflictException($"Draft was changed elsewhere (now version {draft.Version}). Reload before saving.");
        try
        {
            draft.Update(request.Recipient, request.Subject, request.Body, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
        }
        catch (ArgumentException ex) { throw Invalid("body", ex.Message); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Draft was changed elsewhere. Reload before saving."); }
        return ToDto(draft);
    }

    public async Task<DraftDto> ApproveAsync(Guid id, ApproveDraftRequest request, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        if (request is null || request.Version != draft.Version)
            throw new ConflictException($"Draft was changed elsewhere (now version {draft.Version}). Reload before approving.");
        try
        {
            draft.Approve(request.Version, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { throw Invalid("draft", ex.Message); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Draft was changed elsewhere. Reload before approving."); }
        return ToDto(draft);
    }

    public async Task<DraftDto> RevokeAsync(Guid id, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        draft.RevokeApproval(clock.GetUtcNow().UtcDateTime);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Draft was changed elsewhere. Reload and try again."); }
        return ToDto(draft);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        db.OutreachDrafts.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<OutreachDraft> Owned => db.OutreachDrafts.Where(d => d.OwnerId == user.OwnerId);

    private async Task<OutreachDraft> FindAsync(Guid id, CancellationToken ct) =>
        await Owned.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Draft not found.");

    private async Task EnsureOpportunityAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Opportunities.AnyAsync(o => o.Id == id && o.OwnerId == user.OwnerId, ct))
            throw new NotFoundException("Opportunity not found.");
    }

    public static DraftDto ToDto(OutreachDraft draft)
    {
        var validApproval = draft.HasValidApproval();
        var state = validApproval ? DraftState.Approved : DraftState.Draft;
        var blockers = new List<string> { "Sending arrives with Gmail (M6)." };
        if (!validApproval) blockers.Add("Approve this exact draft before the agent can use it.");
        return new DraftDto(draft.Id, draft.OpportunityId, draft.Channel, draft.Recipient, draft.RecipientVerified,
            draft.Subject, draft.Body, draft.Version, state, validApproval ? draft.ApprovedVersion : null,
            validApproval ? draft.ApprovedAt : null, draft.Source, null, Claims(draft.ClaimsJson),
            false, blockers, draft.CreatedAt, draft.UpdatedAt);
    }

    private static (string Body, IReadOnlyList<DraftClaimDto> Claims) GenerateCoverNote(
        Opportunity opportunity, string criteriaJson, string profileJson)
    {
        var profile = Object(profileJson);
        var facts = Deserialize<List<FactRow>>(opportunity.FactsJson) ?? [];
        var breakdown = Deserialize<List<BreakdownRow>>(opportunity.BreakdownJson) ?? [];
        var criteria = CampaignCriteria.FromJson(criteriaJson);
        var claims = new List<DraftClaimDto>();
        var evidence = facts.FirstOrDefault(f => !f.IsInference && Guid.TryParse(f.EvidenceId, out _))?.EvidenceId
            ?? breakdown.SelectMany(b => b.EvidenceIds).FirstOrDefault(id => Guid.TryParse(id, out _));
        Guid? EvidenceId(string? raw) => Guid.TryParse(raw, out var id) ? id : null;

        var verifiedOrganization = facts.FirstOrDefault(f => f.Key == "organization" && !f.IsInference);
        var organization = verifiedOrganization?.Value?.Trim();
        var greeting = string.IsNullOrWhiteSpace(organization)
            ? "Dear Hiring Manager,"
            : $"Dear Hiring Team at {organization},";

        var title = opportunity.Title.Trim();
        var intro = $"I am applying for the {title} role.";
        if (EvidenceId(evidence) is { } titleEvidence) claims.Add(new DraftClaimDto(intro, "Evidence", titleEvidence));

        var paragraphs = new List<string> { greeting, string.Empty, intro };
        var skillFact = facts.FirstOrDefault(f => f.Key == "requiredSkillsFound" && !f.IsInference);
        if (skillFact is not null && !string.IsNullOrWhiteSpace(skillFact.Value))
        {
            var sentence = $"The role calls for skills I match: {skillFact.Value}.";
            paragraphs.Add(sentence);
            if (EvidenceId(skillFact.EvidenceId) is { } skillEvidence) claims.Add(new DraftClaimDto(sentence, "Evidence", skillEvidence));
        }

        if (criteria.CandidateYears is { } years)
        {
            var text = years.ToString("0.#", CultureInfo.InvariantCulture);
            var sentence = $"My confirmed experience for this campaign is {text} years.";
            paragraphs.Add(sentence);
            claims.Add(new DraftClaimDto(sentence, "Profile", null));
        }

        if (String(profile, "offer", "summary", "professionalSummary") is { } offer)
        {
            paragraphs.Add(offer);
            claims.Add(new DraftClaimDto(offer, "Profile", null));
        }
        else paragraphs.Add("[Add your confirmed professional summary here.] ");

        if (String(profile, "availability", "noticePeriod", "startDate") is { } availability)
        {
            var sentence = $"Availability: {availability}";
            paragraphs.Add(sentence);
            claims.Add(new DraftClaimDto(sentence, "Profile", null));
        }

        paragraphs.AddRange([string.Empty, "Thank you for considering my application.", string.Empty,
            "Sincerely,", String(profile, "fullName", "candidateName", "name") ?? "[Your name]"]);
        return (string.Join(Environment.NewLine, paragraphs), claims);
    }

    private static JsonElement Object(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : default;
        }
        catch (JsonException) { return default; }
    }

    private static string? String(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var candidate in new[] { root }.Concat(
                     root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object ? [fields] : []))
            foreach (var name in names)
                if (candidate.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!.Trim();
        return null;
    }

    private static IReadOnlyList<DraftClaimDto> Claims(string json) => Deserialize<List<DraftClaimDto>>(json) ?? [];
    private static T? Deserialize<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

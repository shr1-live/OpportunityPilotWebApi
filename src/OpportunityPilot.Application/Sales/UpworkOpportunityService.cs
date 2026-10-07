using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Application.Sales;

public sealed class UpworkOpportunityService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<IReadOnlyList<UpworkOpportunityDto>> ListAsync(UpworkOpportunityState? state, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        var query = Owned;
        if (state is { } requested) query = query.Where(x => x.State == requested);
        return (await query.OrderByDescending(x => x.ObservedAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    public async Task<UpworkOpportunityDto> ImportAsync(ImportUpworkOpportunityRequest request, CancellationToken ct)
    {
        Validate(request);
        var now = Now;
        var existing = await Owned.FirstOrDefaultAsync(x => x.ProviderJobId == request.ProviderJobId.Trim(), ct);
        if (existing is null)
        {
            try
            {
                existing = new UpworkOpportunity(user.OwnerId, request.ProviderJobId, request.Title, request.Url,
                    request.ObservedAt?.ToUniversalTime() ?? now, now);
                db.UpworkOpportunities.Add(existing);
            }
            catch (ArgumentException ex) { throw Invalid("opportunity", ex.Message); }
        }

        try
        {
            existing.UpdateResearch(request.Summary, request.Location, request.BudgetType, request.BudgetMin,
                request.BudgetMax, request.Currency, request.ExperienceLevel, request.ConnectsRequired,
                request.AvailableConnectsAtReview, request.PaymentVerified, request.PostedAt?.ToUniversalTime(),
                request.ObservedAt?.ToUniversalTime() ?? now, request.EvidenceJson ?? "{}", now);
        }
        catch (ArgumentException ex) { throw Invalid("opportunity", ex.Message); }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("This Upwork job was imported at the same time. Reload the queue."); }
        return ToDto(existing);
    }

    public async Task<UpworkOpportunityDto> DecideAsync(Guid id, DecideUpworkOpportunityRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        var opportunity = await FindAsync(id, ct);
        try { opportunity.ChangeState(request.State, request.ExpectedVersion, Now); }
        catch (ArgumentOutOfRangeException) { throw Invalid("state", "Choose Saved, Shortlisted or Dismissed."); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        await db.SaveChangesAsync(ct);
        return ToDto(opportunity);
    }

    public async Task<SalesProjectDto> PromoteAsync(Guid id, CancellationToken ct)
    {
        var opportunity = await FindAsync(id, ct);
        if (opportunity.State != UpworkOpportunityState.Shortlisted)
            throw new ConflictException("Shortlist this Upwork job before preparing a bid.");
        if (opportunity.SalesProjectId is { } existingId)
            return await SalesProjectDtoAsync(existingId, ct);

        var project = new SalesProject(user.OwnerId, SalesProjectSource.Upwork, opportunity.ProviderJobId,
            opportunity.Title, Now);
        project.UpdateDetails(null, opportunity.Summary, opportunity.Url, null, opportunity.EvidenceJson, Now);
        db.SalesProjects.Add(project);
        opportunity.MarkPromoted(project.Id, Now);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("This Upwork job already has a sales project. Reload the queue."); }
        return new SalesProjectDto(project.Id, project.Source, project.ExternalId, project.Title, project.Buyer,
            project.Description, project.Url, project.DeadlineUtc, project.EvidenceJson, project.State,
            project.Version, [], project.CreatedAt, project.UpdatedAt);
    }

    private IQueryable<UpworkOpportunity> Owned => db.UpworkOpportunities.Where(x => x.OwnerId == user.OwnerId);

    private async Task<UpworkOpportunity> FindAsync(Guid id, CancellationToken ct) =>
        await Owned.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Upwork opportunity not found.");

    private async Task<SalesProjectDto> SalesProjectDtoAsync(Guid id, CancellationToken ct)
    {
        var project = await db.SalesProjects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Sales project not found.");
        return new(project.Id, project.Source, project.ExternalId, project.Title, project.Buyer, project.Description,
            project.Url, project.DeadlineUtc, project.EvidenceJson, project.State, project.Version, [],
            project.CreatedAt, project.UpdatedAt);
    }

    private static void Validate(ImportUpworkOpportunityRequest request)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ProviderJobId)) errors["providerJobId"] = ["Provider job ID is required."];
        if (string.IsNullOrWhiteSpace(request.Title)) errors["title"] = ["Title is required."];
        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) errors["url"] = ["A valid HTTP(S) job URL is required."];
        if (!Enum.IsDefined(request.BudgetType)) errors["budgetType"] = ["Unknown budget type."];
        if (request.EvidenceJson is { Length: > 0 })
        {
            try { using var _ = JsonDocument.Parse(request.EvidenceJson); }
            catch (JsonException) { errors["evidenceJson"] = ["Evidence must be valid JSON."]; }
        }
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    private static UpworkOpportunityDto ToDto(UpworkOpportunity x) => new(
        x.Id, x.ProviderJobId, x.Title, x.Url, x.Summary, x.Location, x.BudgetType, x.BudgetMin, x.BudgetMax,
        x.Currency, x.ExperienceLevel, x.ConnectsRequired, x.AvailableConnectsAtReview, ConnectsStatus(x),
        x.PaymentVerified, x.PostedAt, x.ObservedAt, x.EvidenceJson, x.State, x.SalesProjectId, x.Version,
        x.CreatedAt, x.UpdatedAt);

    private static string ConnectsStatus(UpworkOpportunity x) =>
        x.ConnectsRequired is null || x.AvailableConnectsAtReview is null ? "Unknown" :
        x.AvailableConnectsAtReview >= x.ConnectsRequired ? "Sufficient" : "Insufficient";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}


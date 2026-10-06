using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Application.Sales;

public sealed class SalesService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<IReadOnlyList<SalesProjectDto>> ListProjectsAsync(int take, int skip, SalesProjectState? state, SalesProjectSource? source, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);
        var query = OwnedProjects;
        if (state is { } requestedState) query = query.Where(p => p.State == requestedState);
        if (source is { } requestedSource) query = query.Where(p => p.Source == requestedSource);
        var projects = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToArray();
        var bids = await db.SalesBids.Where(b => b.OwnerId == user.OwnerId && ids.Contains(b.ProjectId))
            .OrderByDescending(b => b.UpdatedAt).ToListAsync(ct);
        return projects.Select(p => ToDto(p, bids.Where(b => b.ProjectId == p.Id))).ToList();
    }

    public async Task<SalesProjectDto> GetProjectAsync(Guid id, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        var bids = await db.SalesBids.Where(b => b.OwnerId == user.OwnerId && b.ProjectId == id)
            .OrderByDescending(b => b.UpdatedAt).ToListAsync(ct);
        return ToDto(project, bids);
    }

    public async Task<SalesProjectDto> CreateProjectAsync(CreateSalesProjectRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        if (request.Source != SalesProjectSource.Manual)
            throw Invalid("source", "Only manually entered sales projects are available until a provider is configured.");
        if (string.IsNullOrWhiteSpace(request.Title)) throw Invalid("title", "Title is required.");
        SalesProject project;
        try
        {
            project = new SalesProject(user.OwnerId, request.Source, request.ExternalId, request.Title, Now);
            project.UpdateDetails(request.Buyer, request.Description, request.Url, request.DeadlineUtc,
                string.IsNullOrWhiteSpace(request.EvidenceJson) ? "[]" : request.EvidenceJson, Now);
        }
        catch (ArgumentException ex) { throw Invalid("project", ex.Message); }
        db.SalesProjects.Add(project);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A project with this provider id already exists."); }
        return ToDto(project, []);
    }

    public async Task<SalesProjectDto> CreateBidAsync(Guid projectId, CreateSalesBidRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        var project = await FindProjectAsync(projectId, ct);
        SalesBid bid;
        try { bid = new SalesBid(user.OwnerId, project.Id, request.Amount, request.Currency, request.DeliveryDays, request.Proposal, Now); }
        catch (ArgumentException ex) { throw Invalid("bid", ex.Message); }
        db.SalesBids.Add(bid);
        if (project.State is SalesProjectState.New or SalesProjectState.Shortlisted) project.ChangeState(SalesProjectState.BidPrepared, Now);
        try { await db.SaveChangesAsync(ct); }
        catch (ArgumentException ex) { throw Invalid("bid", ex.Message); }
        catch (DbUpdateException) { throw new ConflictException("The bid could not be saved."); }
        return await GetProjectAsync(project.Id, ct);
    }

    public async Task<SalesProjectDto> UpdateBidAsync(Guid bidId, UpdateSalesBidRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("body", "Request body is required.");
        var bid = await FindBidAsync(bidId, ct);
        try { bid.Update(request.ExpectedVersion, request.Amount, request.Currency, request.DeliveryDays, request.Proposal, Now); }
        catch (ArgumentException ex) { throw Invalid("bid", ex.Message); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        await db.SaveChangesAsync(ct);
        return await GetProjectAsync(bid.ProjectId, ct);
    }

    public async Task<SalesProjectDto> ApproveBidAsync(Guid bidId, ApproveSalesBidRequest request, CancellationToken ct)
    {
        if (request is null) throw Invalid("version", "Version is required.");
        var bid = await FindBidAsync(bidId, ct);
        try { bid.Approve(request.Version, Now); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        var project = await FindProjectAsync(bid.ProjectId, ct);
        project.ChangeState(SalesProjectState.BidApproved, Now);
        await db.SaveChangesAsync(ct);
        return await GetProjectAsync(project.Id, ct);
    }

    private IQueryable<SalesProject> OwnedProjects => db.SalesProjects.Where(p => p.OwnerId == user.OwnerId);

    private async Task<SalesProject> FindProjectAsync(Guid id, CancellationToken ct) =>
        await OwnedProjects.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Sales project not found.");

    private async Task<SalesBid> FindBidAsync(Guid id, CancellationToken ct) =>
        await db.SalesBids.FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == user.OwnerId, ct)
        ?? throw new NotFoundException("Sales bid not found.");

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static SalesProjectDto ToDto(SalesProject project, IEnumerable<SalesBid> bids) =>
        new(project.Id, project.Source, project.ExternalId, project.Title, project.Buyer, project.Description, project.Url,
            project.DeadlineUtc, project.State, project.Version, bids.Select(ToDto).ToList(), project.CreatedAt, project.UpdatedAt);

    private static SalesBidDto ToDto(SalesBid bid) =>
        new(bid.Id, bid.ProjectId, bid.Amount, bid.Currency, bid.DeliveryDays, bid.Proposal, bid.Version, bid.State,
            bid.HasValidApproval() ? bid.ApprovedVersion : null, bid.HasValidApproval() ? bid.ApprovedAt : null,
            bid.HasValidApproval(), bid.CreatedAt, bid.UpdatedAt);

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

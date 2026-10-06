using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.UnitTests;

public sealed class SalesBidTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Approval_is_bound_to_the_exact_bid_version()
    {
        var bid = new SalesBid(OwnerId, ProjectId, 1250m, "usd", 14, "Proposal text", Now);

        bid.Approve(1, Now.AddMinutes(1));

        Assert.Equal(SalesBidState.Approved, bid.State);
        Assert.True(bid.HasValidApproval());
    }

    [Fact]
    public void Editing_a_bid_clears_approval_and_increments_version()
    {
        var bid = new SalesBid(OwnerId, ProjectId, 1250m, "USD", 14, "Proposal text", Now);
        bid.Approve(1, Now.AddMinutes(1));

        bid.Update(1, 1500m, "USD", 18, "Updated proposal", Now.AddMinutes(2));

        Assert.Equal(2, bid.Version);
        Assert.Equal(SalesBidState.Draft, bid.State);
        Assert.False(bid.HasValidApproval());
    }

    [Fact]
    public void Stale_bid_update_is_rejected()
    {
        var bid = new SalesBid(OwnerId, ProjectId, 1250m, "USD", 14, "Proposal text", Now);
        bid.Update(1, 1500m, "USD", 18, "Updated proposal", Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => bid.Update(1, 1750m, "USD", 21, "Another proposal", Now.AddMinutes(2)));
    }

    [Fact]
    public void Placement_requires_a_valid_approval()
    {
        var bid = new SalesBid(OwnerId, ProjectId, 1250m, "USD", 14, "Proposal text", Now);

        Assert.Throws<InvalidOperationException>(() => bid.MarkPlaced(Now.AddMinutes(1)));
        bid.Approve(1, Now.AddMinutes(2));
        bid.MarkPlaced(Now.AddMinutes(3));

        Assert.Equal(SalesBidState.Placed, bid.State);
    }
}

using OpportunityPilot.Domain.Drafts;

namespace OpportunityPilot.UnitTests;

public class OutreachDraftTests
{
    private static readonly DateTime T0 = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Editing_an_approved_draft_increments_version_and_clears_approval()
    {
        var draft = new OutreachDraft(Guid.NewGuid(), Guid.NewGuid(), DraftChannel.CoverNote, null, false, null,
            "Original", DraftSource.Template, "[]", T0);
        draft.Approve(1, T0.AddMinutes(1));

        Assert.True(draft.HasValidApproval());
        draft.Update(null, false, null, "Edited", T0.AddMinutes(2));

        Assert.Equal(2, draft.Version);
        Assert.Equal(DraftState.Draft, draft.State);
        Assert.Null(draft.ApprovedHash);
        Assert.False(draft.HasValidApproval());
    }

    [Fact]
    public void Stale_version_cannot_be_approved()
    {
        var draft = new OutreachDraft(Guid.NewGuid(), Guid.NewGuid(), DraftChannel.CoverNote, null, false, null,
            "Body", DraftSource.Template, "[]", T0);

        Assert.Throws<InvalidOperationException>(() => draft.Approve(0, T0));
    }
}

using OpportunityPilot.Application.Drafts;

namespace OpportunityPilot.UnitTests;

public class LinkedInNoteTests
{
    [Fact]
    public void Short_offer_is_kept_whole()
    {
        var note = LinkedInNote.Build("Acme", ".NET Developer", "We build .NET delivery teams", "Asha Rao");
        Assert.Equal("Hi, I saw Acme is hiring for .NET Developer. We build .NET delivery teams. I'd like to connect. Asha Rao", note);
    }

    [Fact]
    public void Long_offer_is_cut_at_a_word_and_the_note_never_exceeds_the_limit()
    {
        var offer = string.Join(' ', Enumerable.Repeat("Backend and integration work in .NET for logistics firms", 12));
        var note = LinkedInNote.Build("Acme Logistics", "Senior .NET Engineer", offer, "Asha Rao");
        Assert.True(note.Length <= LinkedInNote.MaxLength);
        Assert.EndsWith("I'd like to connect. Asha Rao", note);
        Assert.Contains("…", note);
    }

    [Fact]
    public void Placeholder_offer_is_left_out_but_the_name_placeholder_stays_to_block_approval()
    {
        var note = LinkedInNote.Build("Acme", "Dev", "[Add your confirmed offer here.]", "[Your name]");
        Assert.DoesNotContain("confirmed offer", note);
        Assert.Contains("[Your name]", note);
    }
}

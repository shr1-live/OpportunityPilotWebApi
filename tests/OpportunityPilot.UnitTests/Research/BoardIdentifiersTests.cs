using OpportunityPilot.Application.Research.Boards;

namespace OpportunityPilot.UnitTests.Research;

public class BoardIdentifiersTests
{
    [Theory]
    [InlineData("stripe", "stripe")]
    [InlineData("  Stripe ", "stripe")]
    [InlineData("acme-corp-2", "acme-corp-2")]
    [InlineData("https://boards.greenhouse.io/stripe", "stripe")]
    [InlineData("https://job-boards.greenhouse.io/stripe", "stripe")]
    [InlineData("http://boards.greenhouse.io/stripe/", "stripe")]
    [InlineData("boards.greenhouse.io/stripe", "stripe")]
    [InlineData("https://www.boards.greenhouse.io/stripe", "stripe")]
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/7171717?gh_jid=7171717", "stripe")]
    [InlineData("https://boards.greenhouse.io/embed/job_board?for=Stripe&b=https://stripe.com", "stripe")]
    public void Greenhouse_tokens_and_board_urls_normalise_to_the_token(string input, string expected) =>
        Assert.Equal(expected, BoardIdentifiers.Greenhouse(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("stripe inc")]                                       // space
    [InlineData("stripe_inc")]                                       // underscore is outside [a-z0-9-]
    [InlineData("str/ipe")]
    [InlineData("https://evil.example/stripe")]                      // another host is never accepted
    [InlineData("https://boards.greenhouse.io.evil.example/stripe")]
    [InlineData("https://jobs.lever.co/stripe")]                     // a Lever URL is not a Greenhouse board
    [InlineData("https://boards.greenhouse.io/")]                    // no token
    [InlineData("https://boards.greenhouse.io:8443/stripe")]         // non-default port
    [InlineData("https://user:pw@boards.greenhouse.io/stripe")]
    [InlineData("ftp://boards.greenhouse.io/stripe")]
    [InlineData("https://boards.greenhouse.io/embed/job_board")]    // embed without ?for=
    public void Invalid_greenhouse_input_is_refused(string? input) =>
        Assert.Null(BoardIdentifiers.Greenhouse(input));

    [Fact]
    public void Greenhouse_tokens_longer_than_100_characters_are_refused()
    {
        Assert.Equal(new string('a', 100), BoardIdentifiers.Greenhouse(new string('a', 100)));
        Assert.Null(BoardIdentifiers.Greenhouse(new string('a', 101)));
        Assert.Null(BoardIdentifiers.Greenhouse("https://boards.greenhouse.io/" + new string('a', 101)));
    }

    [Theory]
    [InlineData("leverdemo", "leverdemo")]
    [InlineData("LeverDemo", "leverdemo")]
    [InlineData("https://jobs.lever.co/leverdemo", "leverdemo")]
    [InlineData("jobs.lever.co/leverdemo/5ac21346-8e0c-4494-8e7a-3eb92ff77902", "leverdemo")]
    [InlineData("https://jobs.lever.co/acme-labs/5ac21346-8e0c-4494-8e7a-3eb92ff77902/apply", "acme-labs")]
    public void Lever_slugs_and_urls_normalise_to_the_slug(string input, string expected) =>
        Assert.Equal(expected, BoardIdentifiers.Lever(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lever demo")]
    [InlineData("lever.demo")]
    [InlineData("https://api.lever.co/v0/postings/leverdemo")]      // the API host is not a board URL
    [InlineData("https://boards.greenhouse.io/leverdemo")]
    [InlineData("https://jobs.lever.co/")]
    [InlineData("https://jobs.lever.co.evil.example/leverdemo")]
    public void Invalid_lever_input_is_refused(string? input) =>
        Assert.Null(BoardIdentifiers.Lever(input));

    [Theory]
    [InlineData("ashby", "https://jobs.ashbyhq.com/ashby", "ashby")]
    [InlineData("smartrecruiters", "https://careers.smartrecruiters.com/smartrecruiters", "smartrecruiters")]
    [InlineData("recruitee", "https://recruiteedemo.recruitee.com/o/developer", "recruiteedemo")]
    [InlineData("workable", "https://apply.workable.com/acme/jobs/", "acme")]
    [InlineData("workable-subdomain", "https://acme.workable.com/", "acme")]
    public void Additional_board_urls_are_reduced_to_safe_slugs(string kind, string input, string expected)
    {
        var actual = kind switch
        {
            "ashby" => BoardIdentifiers.Ashby(input),
            "smartrecruiters" => BoardIdentifiers.SmartRecruiters(input),
            "recruitee" => BoardIdentifiers.Recruitee(input),
            _ => BoardIdentifiers.Workable(input)
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Additional_boards_refuse_untrusted_hosts()
    {
        Assert.Null(BoardIdentifiers.Ashby("https://evil.example/ashby"));
        Assert.Null(BoardIdentifiers.SmartRecruiters("https://careers.smartrecruiters.com.evil.example/acme"));
        Assert.Null(BoardIdentifiers.Recruitee("https://acme.recruitee.com.evil.example"));
        Assert.Null(BoardIdentifiers.Workable("https://evil.example/workable"));
    }
}

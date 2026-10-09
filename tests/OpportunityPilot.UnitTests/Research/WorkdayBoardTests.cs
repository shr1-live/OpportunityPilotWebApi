using OpportunityPilot.Application.Research;
using OpportunityPilot.Application.Research.Boards;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.UnitTests.Research;

public class WorkdayBoardTests
{
    [Theory]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/NVIDIAExternalCareerSite", "nvidia.wd5/NVIDIAExternalCareerSite")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/NVIDIAExternalCareerSite/job/India/Engineer_R1", "nvidia.wd5/NVIDIAExternalCareerSite")]
    [InlineData("nvidia.wd5/NVIDIAExternalCareerSite", "nvidia.wd5/NVIDIAExternalCareerSite")]
    public void Public_careers_urls_are_normalized(string input, string expected) =>
        Assert.Equal(expected, WorkdayBoard.Parse(input)?.ToString());

    [Theory]
    [InlineData("https://evil.example/NVIDIAExternalCareerSite")]
    [InlineData("http://nvidia.wd5.myworkdayjobs.com/NVIDIAExternalCareerSite")]
    [InlineData("https://user:password@nvidia.wd5.myworkdayjobs.com/NVIDIAExternalCareerSite")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/login")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/NVIDIAExternalCareerSite/account/settings")]
    [InlineData("nvidia.wd5/jobs")]
    public void Non_public_or_account_urls_are_rejected(string input) => Assert.Null(WorkdayBoard.Parse(input));

    [Fact]
    public void Search_and_detail_json_map_only_verified_workday_fields()
    {
        var list = WorkdayMapping.Postings("""{"total":2,"jobPostings":[{"title":"Engineer","externalPath":"/job/India/Engineer_R1","locationsText":"Remote","bulletFields":["R1"]},{"title":"Bad","externalPath":"https://evil.example/job"}]}""");
        var posting = Assert.Single(list!.Value.Postings);
        Assert.Equal(("Engineer", "R1"), (posting.Title, posting.RequisitionId));

        var detail = WorkdayMapping.Detail("""{"jobPostingInfo":{"jobDescription":"<p>C# and Azure</p>","location":"Remote - India","startDate":"2026-10-01","externalUrl":"https://evil.example/job","jobReqId":"R1"}}""", new ContentParser());
        var source = new Source(Guid.NewGuid(), Guid.NewGuid(), SourceKind.Workday, "Workday", "acme.wd5/External", null, null, null,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var candidate = WorkdayMapping.Candidate(source, WorkdayBoard.Parse(source.Url)!, posting, detail);

        Assert.Equal(JobPlatform.Workday, candidate.Platform);
        Assert.Equal("R1", candidate.ExternalId);
        Assert.Equal("C# and Azure", candidate.Text);
        Assert.StartsWith("https://acme.wd5.myworkdayjobs.com/External/job/", candidate.ApplyUrl);
    }
}

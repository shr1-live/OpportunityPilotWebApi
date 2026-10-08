using OpportunityPilot.Application.JobBoards;

namespace OpportunityPilot.UnitTests;

public class JsearchBoardParserTests
{
    private const string Json = """
    {"status":"OK","data":[
      {"job_id":"a1","job_title":".NET Developer","employer_name":"Acme","job_publisher":"Indeed",
       "job_apply_link":"https://www.indeed.com/viewjob?jk=abc","job_city":"Austin","job_state":"TX","job_country":"US",
       "job_is_remote":true,"job_employment_type":"FULLTIME","job_min_salary":90000,"job_max_salary":120000,
       "job_salary_currency":"USD","job_salary_period":"YEAR","job_posted_at_datetime_utc":"2026-10-07T10:00:00.000Z"},
      {"job_id":"b2","job_title":"Backend Engineer","employer_name":"Beta","job_publisher":"LinkedIn",
       "job_apply_link":"https://www.linkedin.com/jobs/view/1",
       "apply_options":[{"publisher":"LinkedIn","apply_link":"https://www.linkedin.com/jobs/view/1"},
                        {"publisher":"Indeed","apply_link":"https://in.indeed.com/viewjob?jk=def"}]},
      {"job_id":"c3","job_title":"QA","employer_name":"Gamma","job_publisher":"Glassdoor","job_apply_link":"https://glassdoor.com/x"},
      {"job_id":"d4","job_title":"Fake","employer_name":"Evil","job_publisher":"Indeed","job_apply_link":"https://indeed.com.evil.example/x"},
      {"job_id":"e5","job_title":"Data Analyst","employer_name":"Delta","job_publisher":"SEEK","job_apply_link":"https://www.seek.com.au/job/123"},
      {"job_id":"f6","job_title":"Plain http","employer_name":"Eps","job_publisher":"LinkedIn","job_apply_link":"http://www.linkedin.com/jobs/view/2"},
      {"job_id":"a1","job_title":".NET Developer","employer_name":"Acme","job_publisher":"Indeed","job_apply_link":"https://www.indeed.com/viewjob?jk=abc"}
    ]}
    """;

    [Theory]
    [InlineData(JobBoard.Indeed, new[] { "a1", "b2" })]
    [InlineData(JobBoard.LinkedIn, new[] { "b2" })]
    [InlineData(JobBoard.Seek, new[] { "e5" })]
    public void Keeps_only_postings_with_a_real_link_on_that_board(JobBoard board, string[] ids)
    {
        var (jobs, total) = JsearchBoardParser.Parse(Json, board);
        Assert.Equal(7, total);
        Assert.Equal(ids, jobs.Select(j => j.ProviderJobId));
    }

    [Fact]
    public void Uses_the_board_apply_option_when_the_publisher_differs()
    {
        var (jobs, _) = JsearchBoardParser.Parse(Json, JobBoard.Indeed);
        Assert.Equal("https://in.indeed.com/viewjob?jk=def", jobs[1].BoardUrl);
    }

    [Fact]
    public void Maps_fields_without_inventing_values()
    {
        var (jobs, _) = JsearchBoardParser.Parse(Json, JobBoard.Indeed);
        var a = jobs[0];
        Assert.Equal("Austin, TX, US", a.Location);
        Assert.True(a.IsRemote);
        Assert.Equal(90000m, a.SalaryMin);
        Assert.Equal("USD", a.SalaryCurrency);
        Assert.Equal(new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc), a.PostedAt);
        Assert.Null(jobs[1].SalaryMin);
        Assert.Null(jobs[1].PostedAt);
    }

    [Fact]
    public void Missing_data_gives_no_jobs() => Assert.Empty(JsearchBoardParser.Parse("""{"status":"ERROR"}""", JobBoard.Indeed).Jobs);
}

namespace OpportunityPilot.UnitTests;

public class ResolveProviderTests
{
    [Theory]
    [InlineData("Postgress", "postgresql://postgres.ref:pw@aws-0-ap-northeast-2.pooler.supabase.com:5432/postgres")]
    [InlineData(null, "Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres")]
    [InlineData("postgres", "Host=localhost;Database=x")]
    public void Postgres_is_used_when_the_connection_string_or_setting_says_so(string? configured, string cs) =>
        Assert.Equal("Postgres", OpportunityPilot.Infrastructure.DependencyInjection.ResolveProvider(configured, cs));

    [Fact]
    public void Sql_server_stays_the_default_for_local_db() =>
        Assert.Equal("SqlServer", OpportunityPilot.Infrastructure.DependencyInjection.ResolveProvider(null, @"Server=(localdb)\MSSQLLocalDB;Database=x"));

    [Fact]
    public void A_typo_without_a_recognisable_connection_string_still_fails_clearly() =>
        Assert.Throws<InvalidOperationException>(() => OpportunityPilot.Infrastructure.DependencyInjection.ResolveProvider("Postgress", "Server=x;Database=y"));
}

using Npgsql;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.UnitTests;

public class PostgresConnectionStringTests
{
    [Fact]
    public void Key_value_strings_pass_through_unchanged()
    {
        const string cs = "Host=db.local;Database=app;Username=u;Password=p";
        Assert.Equal(cs, PostgresConnectionString.Normalize(cs));
    }

    [Fact]
    public void Supabase_pooler_uri_is_converted_with_encoded_password()
    {
        var cs = PostgresConnectionString.Normalize(
            "postgresql://postgres.abcd:p%40ss%3Aword@aws-0-ap-south-1.pooler.supabase.com:5432/postgres?sslmode=require");
        var b = new NpgsqlConnectionStringBuilder(cs);

        Assert.Equal("aws-0-ap-south-1.pooler.supabase.com", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("postgres", b.Database);
        Assert.Equal("postgres.abcd", b.Username);
        Assert.Equal("p@ss:word", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Render_style_uri_without_port_defaults_to_5432()
    {
        var b = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Normalize("postgres://user:secret@dpg-xyz.singapore-postgres.render.com/opdb"));

        Assert.Equal(5432, b.Port);
        Assert.Equal("opdb", b.Database);
        Assert.Equal("secret", b.Password);
    }
}

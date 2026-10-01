using OpportunityPilot.Domain.Profiles;

namespace OpportunityPilot.UnitTests;

public class ProfileTests
{
    private static readonly DateTime T0 = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_profile_starts_at_version_1()
    {
        var p = new Profile(Guid.NewGuid(), ProfileType.Candidate, "  .NET full-stack  ", "{}", confirmed: false, T0);

        Assert.Equal(1, p.Version);
        Assert.Equal(".NET full-stack", p.Name);
        Assert.Null(p.ConfirmedAt);
    }

    [Fact]
    public void Update_increments_version_and_confirmation_follows_the_latest_save()
    {
        var p = new Profile(Guid.NewGuid(), ProfileType.Product, "Ledgerline", "{}", confirmed: true, T0);
        Assert.Equal(T0, p.ConfirmedAt);

        p.Update("Ledgerline", """{"offer":"changed"}""", confirmed: false, T0.AddHours(1));

        Assert.Equal(2, p.Version);
        Assert.Null(p.ConfirmedAt); // an edited, unconfirmed version is not treated as confirmed
    }

    [Fact]
    public void Owner_and_name_are_required()
    {
        Assert.Throws<ArgumentException>(() => new Profile(Guid.Empty, ProfileType.Product, "x", "{}", false, T0));
        Assert.Throws<ArgumentException>(() => new Profile(Guid.NewGuid(), ProfileType.Product, " ", "{}", false, T0));
    }
}

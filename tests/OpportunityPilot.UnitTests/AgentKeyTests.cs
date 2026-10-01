using System.Text.RegularExpressions;
using OpportunityPilot.Application.Agents;
using OpportunityPilot.Domain.Agents;

namespace OpportunityPilot.UnitTests;

public class AgentKeyTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generated_keys_are_prefixed_unpadded_base64url_and_unique()
    {
        var key = AgentKeyService.GenerateKey();

        Assert.Matches(new Regex("^opk_[A-Za-z0-9_-]{43}$"), key);
        Assert.NotEqual(key, AgentKeyService.GenerateKey());
    }

    [Fact]
    public void Hash_is_lowercase_sha256_hex_and_prefix_is_the_first_12_characters()
    {
        var key = AgentKeyService.GenerateKey();

        Assert.Matches(new Regex("^[0-9a-f]{64}$"), AgentKeyService.HashKey(key));
        Assert.Equal(AgentKeyService.HashKey(key), AgentKeyService.HashKey(key));
        Assert.Equal(key[..12], AgentKeyService.PrefixOf(key));
        Assert.StartsWith("opk_", AgentKeyService.PrefixOf(key));
    }

    [Fact]
    public void Hash_matches_a_known_sha256_vector()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", AgentKeyService.HashKey("abc"));
    }

    [Fact]
    public void Revoke_is_idempotent_and_keeps_the_first_time()
    {
        var key = new AgentKey(Guid.NewGuid(), " Laptop ", new string('a', 64), "opk_abcdefgh", T0);

        key.Revoke(T0.AddMinutes(1));
        key.Revoke(T0.AddMinutes(5));

        Assert.Equal("Laptop", key.Name);
        Assert.Equal(T0.AddMinutes(1), key.RevokedAt);
    }
}

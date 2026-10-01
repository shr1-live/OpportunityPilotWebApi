using System.Net;
using System.Net.Sockets;

namespace OpportunityPilot.Infrastructure.Research;

/// <summary>
/// Which destinations outbound fetches may reach. Production always uses <see cref="StrictFetchAddressPolicy"/>;
/// it is not bound to configuration, so no setting can open private addresses. Tests may register their own.
/// </summary>
public interface IFetchAddressPolicy
{
    bool IsAllowed(IPAddress address);

    bool IsPortAllowed(int port);
}

public sealed class StrictFetchAddressPolicy : IFetchAddressPolicy
{
    public bool IsAllowed(IPAddress address) => AddressClassifier.IsPublic(address);

    public bool IsPortAllowed(int port) => port is 80 or 443;
}

/// <summary>Public-internet classification for SSRF protection. Anything not clearly public unicast is refused.</summary>
public static class AddressClassifier
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false
        };
    }

    private static bool IsPublicV4(byte[] b) => !(
        b[0] == 0                                         // 0.0.0.0/8 "this network"
        || b[0] == 10                                     // 10/8 private
        || (b[0] == 100 && (b[1] & 0xC0) == 64)           // 100.64/10 carrier-grade NAT
        || b[0] == 127                                    // 127/8 loopback
        || (b[0] == 169 && b[1] == 254)                   // 169.254/16 link-local, incl. cloud metadata 169.254.169.254
        || (b[0] == 172 && (b[1] & 0xF0) == 16)           // 172.16/12 private
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)        // 192.0.0/24 IETF protocol assignments
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)        // 192.0.2/24 documentation
        || (b[0] == 192 && b[1] == 88 && b[2] == 99)      // 192.88.99/24 6to4 relay anycast
        || (b[0] == 192 && b[1] == 168)                   // 192.168/16 private
        || (b[0] == 198 && (b[1] & 0xFE) == 18)           // 198.18/15 benchmarking
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)     // 198.51.100/24 documentation
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)      // 203.0.113/24 documentation
        || b[0] >= 224);                                  // 224/4 multicast, 240/4 reserved, 255.255.255.255 broadcast

    private static bool IsPublicV6(IPAddress address)
    {
        var b = address.GetAddressBytes();
        // Only global unicast 2000::/3 is ever public. This alone excludes ::, ::1, IPv4-compatible ::a.b.c.d,
        // fc00::/7 unique-local, fe80::/10 link-local, fec0::/10 site-local, ff00::/8 multicast and 100::/64 discard.
        if ((b[0] & 0xE0) != 0x20) return false;
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) return false;   // 2001:db8::/32 documentation
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) return false;   // 2001::/32 Teredo tunnelling
        if (b[0] == 0x20 && b[1] == 0x02)                                                  // 2002::/16 6to4 embeds an IPv4 address
            return IsPublicV4([b[2], b[3], b[4], b[5]]);
        return true;
    }
}

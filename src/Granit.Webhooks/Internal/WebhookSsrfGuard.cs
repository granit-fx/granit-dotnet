using System.Net;
using System.Net.Sockets;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Runtime SSRF protection for webhook delivery HTTP calls.
/// Blocks connections to private, loopback, and link-local IP addresses,
/// preventing DNS rebinding attacks where a hostname resolves to a safe IP
/// at validation time but to an internal IP at delivery time.
/// </summary>
internal static class WebhookSsrfGuard
{
    /// <summary>
    /// Returns <c>true</c> if the given IP address must be blocked for outbound webhook delivery.
    /// Covers loopback, private (RFC 1918), link-local, IPv6 ULA, and IPv4-mapped-IPv6 addresses.
    /// </summary>
    internal static bool IsBlockedIpAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedIPv4(ip.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsBlockedIPv6(ip.GetAddressBytes()),
            _ => false,
        };
    }

    private static bool IsBlockedIPv4(byte[] bytes) =>
        bytes[0] == 0                                             // 0.0.0.0/8
        || bytes[0] == 10                                         // 10.0.0.0/8
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) // 172.16.0.0/12
        || (bytes[0] == 192 && bytes[1] == 168)                  // 192.168.0.0/16
        || (bytes[0] == 169 && bytes[1] == 254)                  // 169.254.0.0/16 (link-local / cloud metadata)
        || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127); // 100.64.0.0/10 (Carrier-Grade NAT)

    private static bool IsBlockedIPv6(byte[] bytes) =>
        (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)         // fe80::/10 (link-local)
        || ((bytes[0] & 0xfe) == 0xfc);                          // fc00::/7 (unique local)
}

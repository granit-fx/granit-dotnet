using System.Net;
using System.Net.Sockets;

namespace Granit.IpGeolocation.Internal;

/// <summary>
/// Classifies IP addresses so the resolver can short-circuit non-routable addresses (private, loopback,
/// link-local, CGNAT, unique-local) before contacting any provider — a privacy and cost safeguard.
/// </summary>
internal static class IpAddressClassifier
{
    /// <summary>
    /// Returns <c>true</c> when the address is private, loopback, link-local, CGNAT, unique-local, or
    /// unspecified — i.e. not a public address worth geolocating.
    /// </summary>
    public static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return IsPrivateOrReserved(address.MapToIPv4());
            }

            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || address.Equals(IPAddress.IPv6Any))
            {
                return true;
            }

            // Unique Local Address fc00::/7.
            return (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                  // 0.0.0.0/8 (unspecified)
                10 => true,                                 // 10.0.0.0/8 (RFC 1918)
                127 => true,                                // 127.0.0.0/8 (loopback)
                100 when b[1] is >= 64 and <= 127 => true,  // 100.64.0.0/10 (CGNAT)
                169 when b[1] == 254 => true,               // 169.254.0.0/16 (link-local)
                172 when b[1] is >= 16 and <= 31 => true,   // 172.16.0.0/12 (RFC 1918)
                192 when b[1] == 168 => true,               // 192.168.0.0/16 (RFC 1918)
                >= 224 => true,                             // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved, 255.255.255.255 broadcast
                _ => false,
            };
        }

        return false;
    }
}

using System.Net;
using System.Net.Sockets;

namespace Granit.Documents.PublicLinks.Endpoints.Internal;

/// <summary>
/// GDPR-friendly client-IP anonymiser (F18.4). Masks IPv4 addresses to <c>/24</c>
/// and IPv6 addresses to <c>/48</c> — sufficient for abuse-pattern detection on
/// the public-links audit trail without retaining a per-user identifier.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the masking applied by major analytics platforms (Google Analytics
/// "anonymizeIp", Matomo, Plausible). Loopback / link-local / private addresses
/// are masked exactly the same way — no special casing — to keep the call site
/// trivial and audit-friendly.
/// </para>
/// </remarks>
internal static class IpAddressAnonymizer
{
    /// <summary>
    /// Returns the anonymised string representation of <paramref name="address"/>,
    /// or <c>null</c> if the address is <c>null</c> or of an unsupported family.
    /// </summary>
    public static string? Mask(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        // Unwrap IPv4-mapped IPv6 (::ffff:1.2.3.4 → 1.2.3.4) so the mask falls on
        // the correct family.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => MaskIPv4(address),
            AddressFamily.InterNetworkV6 => MaskIPv6(address),
            _ => null,
        };
    }

    /// <summary>
    /// Parses <paramref name="raw"/> as an IP address and returns its anonymised
    /// representation, or <c>null</c> if parsing fails or the address is unsupported.
    /// </summary>
    public static string? Mask(string? raw) =>
        IPAddress.TryParse(raw, out IPAddress? parsed) ? Mask(parsed) : null;

    private static string MaskIPv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        bytes[3] = 0;
        return new IPAddress(bytes).ToString();
    }

    private static string MaskIPv6(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        // /48 = first 6 bytes preserved, last 10 zeroed.
        for (int i = 6; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }
        return new IPAddress(bytes).ToString();
    }
}

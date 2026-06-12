using System.Net;
using System.Net.Sockets;

namespace Granit.IpGeolocation;

/// <summary>
/// Produces a privacy-reduced but still well-formed IP address for exposure to clients (GDPR data
/// minimisation): the host portion is zeroed while the network portion is kept for coarse identification.
/// </summary>
/// <remarks>
/// This is distinct from <c>Granit.Diagnostics.LogRedaction.IpAddress</c>, which emits a non-address
/// redaction token (<c>192.168.1.***</c>) intended for logs. Use <see cref="Mask"/> for contract/API values
/// (a consumer can still parse it), and <c>LogRedaction.IpAddress</c> for anything written to logs/traces.
/// <list type="bullet">
/// <item>IPv4 → last octet zeroed (<c>/24</c>): <c>203.0.113.42</c> → <c>203.0.113.0</c>.</item>
/// <item>IPv6 → kept to <c>/48</c>, remaining bits zeroed: <c>2001:db8:1234:5678::1</c> → <c>2001:db8:1234::</c>.</item>
/// </list>
/// </remarks>
public static class IpMasking
{
    private const int IPv4HostBytes = 1;
    private const int IPv6NetworkBytes = 6; // /48 prefix

    /// <summary>
    /// Masks <paramref name="ipAddress"/> by zeroing its host portion.
    /// </summary>
    /// <param name="ipAddress">The raw IP address, or <c>null</c>/empty.</param>
    /// <returns>The masked address, or <c>null</c> when the input is absent or not a valid IP.</returns>
    public static string? Mask(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out IPAddress? parsed))
        {
            return null;
        }

        byte[] bytes = parsed.GetAddressBytes();

        switch (parsed.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                for (int i = bytes.Length - IPv4HostBytes; i < bytes.Length; i++)
                {
                    bytes[i] = 0;
                }

                break;

            case AddressFamily.InterNetworkV6:
                for (int i = IPv6NetworkBytes; i < bytes.Length; i++)
                {
                    bytes[i] = 0;
                }

                break;

            default:
                return null;
        }

        return new IPAddress(bytes).ToString();
    }
}

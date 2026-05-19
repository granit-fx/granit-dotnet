using System.Collections.Frozen;
using System.Net;
using System.Text.RegularExpressions;

namespace Granit.AI.Tenancy;

/// <summary>
/// Validates AI provider endpoint URLs against an <see cref="AIEndpointPolicy"/> to defeat the
/// SSRF surface introduced by tenant-controlled endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Three layers of defence are coordinated by this validator (audit VULN-002):
/// </para>
/// <list type="number">
///   <item>Static URL inspection (this class) — scheme, port, IDN normalisation, IP literal
///   range checks, cloud metadata blocklist, well-known short-hand IP encodings.</item>
///   <item>DNS resolution + IP revalidation at <em>write time</em> (best effort).</item>
///   <item>Connection-time revalidation via <see cref="GranitSafeConnectCallback"/> — defeats
///   DNS rebinding by re-applying the IP rules on the resolved address before
///   <c>SocketsHttpHandler</c> opens the TCP connection.</item>
/// </list>
/// <para>
/// HttpClients consuming validated endpoints MUST also disable redirect follow
/// (<c>AllowAutoRedirect = false</c>) so a public-validated URL cannot 302 the request to a
/// metadata IP.
/// </para>
/// </remarks>
public static partial class AIEndpointValidator
{
    /// <summary>Cloud metadata hostnames blocked regardless of <see cref="AIEndpointPolicy.AllowPrivateIp"/>.</summary>
    private static readonly FrozenSet<string> BlockedMetadataHostnames = new[]
    {
        "localhost",
        "localhost.localdomain",
        "metadata",
        "metadata.google.internal",
        "metadata.azure.com",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cloud metadata IP literals blocked regardless of policy.</summary>
    private static readonly FrozenSet<string> BlockedMetadataIps = new[]
    {
        "169.254.169.254",       // AWS / Azure / GCP IMDSv1
        "100.100.100.200",       // Alibaba Cloud metadata
        "192.0.0.192",           // Oracle Cloud metadata
        "fd00:ec2::254",         // AWS IMDS IPv6
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Matches dotted-quad IPv4 (e.g. 127.0.0.1) — rejects compact / decimal / octal / hex forms.</summary>
    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedQuadIPv4Regex();

    /// <summary>
    /// Validates the given endpoint string against the supplied policy.
    /// </summary>
    /// <param name="endpoint">User-supplied endpoint URL. <c>null</c> / empty values are valid (they signal "fall through").</param>
    /// <param name="policy">Policy of the calling provider.</param>
    /// <returns>The validation outcome.</returns>
    public static AIEndpointValidationResult Validate(string? endpoint, AIEndpointPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // Empty endpoint means "fall through to the next layer of the cascade" — valid.
            return AIEndpointValidationResult.Ok(resolvedHost: string.Empty);
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            return AIEndpointValidationResult.Fail("endpoint.invalid_uri");
        }

        // RFC 3986 normalisation invariants (cheap defences against URL parsing tricks).
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return AIEndpointValidationResult.Fail("endpoint.userinfo_forbidden");
        }
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return AIEndpointValidationResult.Fail("endpoint.fragment_forbidden");
        }

        // Scheme allow-list.
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return AIEndpointValidationResult.Fail("endpoint.scheme_not_allowed");
        }
        if (policy.RequireHttps && uri.Scheme != Uri.UriSchemeHttps)
        {
            return AIEndpointValidationResult.Fail("endpoint.https_required");
        }

        // Port allow-list (when defined).
        if (policy.AllowedPorts is { Count: > 0 } && !policy.AllowedPorts.Contains(uri.Port))
        {
            return AIEndpointValidationResult.Fail("endpoint.port_not_allowed");
        }

        // IDN normalisation: a Punycode-only difference between Host and IdnHost means a homograph
        // attempt with non-ASCII characters that became different ASCII after encoding.
        string host = uri.Host;
        if (!string.Equals(host, uri.IdnHost, StringComparison.Ordinal))
        {
            // Re-check: the IdnHost is the canonical ASCII form. The Host being different means
            // the user provided a non-ASCII form (e.g. fullwidth Latin). Reject to avoid homograph spoofing.
            return AIEndpointValidationResult.Fail("endpoint.non_ascii_host");
        }

        // Cloud metadata blocklist (hostname literal).
        if (BlockedMetadataHostnames.Contains(host))
        {
            // localhost only rejected when AllowLoopback is false.
            bool isLocalhostName = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                   host.Equals("localhost.localdomain", StringComparison.OrdinalIgnoreCase);
            if (!isLocalhostName || !policy.AllowLoopback)
            {
                return AIEndpointValidationResult.Fail("endpoint.metadata_host_blocked");
            }
        }

        // Cloud metadata blocklist (IP literal — case-insensitive for IPv6 forms).
        if (BlockedMetadataIps.Contains(host))
        {
            return AIEndpointValidationResult.Fail("endpoint.metadata_ip_blocked");
        }

        // If the host is an IP literal, evaluate ranges directly (skip DNS).
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            // Reject short-hand IPv4 encodings (decimal, octal, hex, compact dotted) by requiring
            // a canonical 4-octet dotted-quad. IPAddress.TryParse accepts the other forms.
            if (uri.HostNameType == UriHostNameType.IPv4 && !DottedQuadIPv4Regex().IsMatch(host))
            {
                return AIEndpointValidationResult.Fail("endpoint.non_canonical_ipv4");
            }

            if (!IPAddress.TryParse(host, out IPAddress? ip))
            {
                return AIEndpointValidationResult.Fail("endpoint.invalid_ip");
            }

            AIEndpointValidationResult? rangeViolation = CheckIpRanges(ip, policy);
            if (rangeViolation is not null)
            {
                return rangeViolation;
            }
        }

        return AIEndpointValidationResult.Ok(resolvedHost: host);
    }

    /// <summary>
    /// Re-applies the policy's IP-range rules to a freshly-resolved <see cref="IPAddress"/>.
    /// Used by <see cref="GranitSafeConnectCallback"/> to defeat DNS rebinding at TCP-connect time.
    /// </summary>
    public static AIEndpointValidationResult? CheckIpRanges(IPAddress ip, AIEndpointPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(ip);
        ArgumentNullException.ThrowIfNull(policy);

        if (IPAddress.IsLoopback(ip) && !policy.AllowLoopback)
        {
            return AIEndpointValidationResult.Fail("endpoint.loopback_blocked");
        }

        if (!policy.AllowPrivateIp)
        {
            if (IsBlockedAddress(ip))
            {
                return AIEndpointValidationResult.Fail("endpoint.private_ip_blocked");
            }
        }

        // Cloud metadata literal IPs are blocked regardless of policy.
        if (BlockedMetadataIps.Contains(ip.ToString()))
        {
            return AIEndpointValidationResult.Fail("endpoint.metadata_ip_blocked");
        }

        return null;
    }

    private static bool IsBlockedAddress(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            int b0 = bytes[0];
            int b1 = bytes[1];

            // RFC 1918
            if (b0 == 10)
            {
                return true;
            }
            if (b0 == 172 && b1 >= 16 && b1 <= 31)
            {
                return true;
            }
            if (b0 == 192 && b1 == 168)
            {
                return true;
            }

            // Link-local — covers IMDS 169.254.169.254
            if (b0 == 169 && b1 == 254)
            {
                return true;
            }

            // CGNAT 100.64.0.0/10
            if (b0 == 100 && b1 >= 64 && b1 <= 127)
            {
                return true;
            }

            // 0.0.0.0/8 (this network)
            if (b0 == 0)
            {
                return true;
            }

            // Multicast 224.0.0.0/4
            if (b0 >= 224 && b0 <= 239)
            {
                return true;
            }
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // ::1 loopback handled by IPAddress.IsLoopback above.

            // IPv4-mapped IPv6 (::ffff:0:0/96) — extract the embedded IPv4 and re-check.
            if (ip.IsIPv4MappedToIPv6)
            {
                return IsBlockedAddress(ip.MapToIPv4());
            }

            byte[] bytes = ip.GetAddressBytes();
            int firstByte = bytes[0];

            // fc00::/7 — Unique local address
            if ((firstByte & 0xFE) == 0xFC)
            {
                return true;
            }

            // fe80::/10 — Link-local
            if (firstByte == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return true;
            }

            // Multicast ff00::/8
            if (firstByte == 0xFF)
            {
                return true;
            }

            // Documentation 2001:db8::/32 — IsIPv6Documentation is not exposed on .NET 10's
            // IPAddress, so check manually.
            if (firstByte == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
            {
                return true;
            }

            // ::ffff:x.x.x.x with the IPv4 portion being a metadata IP is covered by IsIPv4MappedToIPv6 above.
        }

        return false;
    }
}

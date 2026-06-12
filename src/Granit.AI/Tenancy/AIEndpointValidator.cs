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
/// Three layers of defence are coordinated by this validator:
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

        AIEndpointValidationResult? shapeViolation = ValidateUriShape(uri, policy);
        if (shapeViolation is not null)
        {
            return shapeViolation;
        }

        string host = uri.Host;
        AIEndpointValidationResult? hostViolation = ValidateHost(uri, host, policy);
        if (hostViolation is not null)
        {
            return hostViolation;
        }

        return AIEndpointValidationResult.Ok(resolvedHost: host);
    }

    /// <summary>Validates the URI's RFC 3986 shape: userinfo/fragment, scheme, and port allow-lists.</summary>
    private static AIEndpointValidationResult? ValidateUriShape(Uri uri, AIEndpointPolicy policy)
    {
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

        return null;
    }

    /// <summary>Validates the host: IDN homograph defence, metadata blocklists, and IP-literal ranges.</summary>
    private static AIEndpointValidationResult? ValidateHost(Uri uri, string host, AIEndpointPolicy policy)
    {
        // IDN normalisation: a Punycode-only difference between Host and IdnHost means a homograph
        // attempt with non-ASCII characters that became different ASCII after encoding.
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
            return ValidateIpLiteral(uri, host, policy);
        }

        return null;
    }

    /// <summary>Validates an IP-literal host: canonical IPv4 form, parseability, and policy ranges.</summary>
    private static AIEndpointValidationResult? ValidateIpLiteral(Uri uri, string host, AIEndpointPolicy policy)
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

        return CheckIpRanges(ip, policy);
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

        if (!policy.AllowPrivateIp && IsBlockedAddress(ip))
        {
            return AIEndpointValidationResult.Fail("endpoint.private_ip_blocked");
        }

        // Cloud metadata literal IPs are blocked regardless of policy.
        if (BlockedMetadataIps.Contains(ip.ToString()))
        {
            return AIEndpointValidationResult.Fail("endpoint.metadata_ip_blocked");
        }

        return null;
    }

    private static bool IsBlockedAddress(IPAddress ip) => ip.AddressFamily switch
    {
        System.Net.Sockets.AddressFamily.InterNetwork => IsBlockedIPv4(ip.GetAddressBytes()),
        System.Net.Sockets.AddressFamily.InterNetworkV6 => IsBlockedIPv6(ip),
        _ => false,
    };

    private static bool IsBlockedIPv4(byte[] bytes)
    {
        int b0 = bytes[0];
        int b1 = bytes[1];

        return b0 switch
        {
            10 => true,                              // RFC 1918 10.0.0.0/8
            172 when b1 is >= 16 and <= 31 => true,  // RFC 1918 172.16.0.0/12
            192 when b1 == 168 => true,              // RFC 1918 192.168.0.0/16
            169 when b1 == 254 => true,              // Link-local — covers IMDS 169.254.169.254
            100 when b1 is >= 64 and <= 127 => true, // CGNAT 100.64.0.0/10
            0 => true,                               // 0.0.0.0/8 (this network)
            >= 224 and <= 239 => true,               // Multicast 224.0.0.0/4
            _ => false,
        };
    }

    private static bool IsBlockedIPv6(IPAddress ip)
    {
        // ::1 loopback handled by IPAddress.IsLoopback above.
        // IPv4-mapped IPv6 (::ffff:0:0/96) — extract the embedded IPv4 and re-check.
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsBlockedIPv4(ip.MapToIPv4().GetAddressBytes());
        }

        byte[] bytes = ip.GetAddressBytes();
        int firstByte = bytes[0];

        // fc00::/7 ULA; fe80::/10 link-local; ff00::/8 multicast; 2001:db8::/32 documentation
        // (IsIPv6Documentation is not exposed on .NET 10's IPAddress, so check manually).
        return (firstByte & 0xFE) == 0xFC
            || (firstByte == 0xFE && (bytes[1] & 0xC0) == 0x80)
            || firstByte == 0xFF
            || (firstByte == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
    }
}

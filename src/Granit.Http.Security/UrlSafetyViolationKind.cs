using Granit.Http.Security.Options;

namespace Granit.Http.Security;

/// <summary>
/// Enumerates the kinds of URL safety violations detected by <see cref="IUrlSafetyValidator"/>.
/// </summary>
public enum UrlSafetyViolationKind
{
    /// <summary>The URL scheme is not in the allowlist.</summary>
    SchemeNotAllowed,

    /// <summary>The host is explicitly blocked (reserved for future use).</summary>
    HostBlocked,

    /// <summary>The host resolves to a private (RFC 1918 / CGNAT / 0.0.0.0-8) IP address.</summary>
    PrivateNetwork,

    /// <summary>The host resolves to a loopback address (127.0.0.0/8 or ::1).</summary>
    Loopback,

    /// <summary>The host resolves to a link-local address (169.254.0.0/16 or fe80::/10).</summary>
    LinkLocal,

    /// <summary>
    /// The host resolves to a well-known cloud metadata endpoint
    /// (AWS IMDS 169.254.169.254 / fd00:ec2::254, GCP, Azure IMDS).
    /// </summary>
    MetadataEndpoint,

    /// <summary>The host resolves to an IPv6 unique-local address (fc00::/7).</summary>
    IPv6UniqueLocal,

    /// <summary>
    /// The host uses a reserved TLD that must never be resolvable on the public internet
    /// (.local, .internal, .localhost, .onion, .test, .example, .invalid).
    /// </summary>
    ReservedTld,

    /// <summary>The URL is not absolute or otherwise malformed.</summary>
    MalformedUrl,

    /// <summary>The URL exceeds <see cref="UrlSafetyOptions.MaxUrlLength"/>.</summary>
    UrlTooLong,

    /// <summary>The host matches a pattern in <see cref="UrlSafetyOptions.DeniedHostPatterns"/>.</summary>
    HostPatternDenied,

    /// <summary>The host does not match any pattern in <see cref="UrlSafetyOptions.AllowedHostPatterns"/>.</summary>
    HostPatternNotAllowed,

    /// <summary>DNS resolution failed or timed out.</summary>
    DnsResolutionFailed,
}

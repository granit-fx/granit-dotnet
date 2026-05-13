using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Security.Options;

/// <summary>
/// Configuration for <see cref="IUrlSafetyValidator"/>.
/// </summary>
public sealed class UrlSafetyOptions
{
    /// <summary>Configuration section name (<c>Http:UrlSafety</c>).</summary>
    public const string SectionName = "Http:UrlSafety";

    /// <summary>
    /// Schemes accepted for validation. Compared case-insensitively against <see cref="Uri.Scheme"/>.
    /// Defaults to <c>["https"]</c>.
    /// </summary>
    /// <remarks>
    /// Enabling <c>"file"</c> bypasses the DNS / IP-classification pipeline and on Windows lets
    /// through UNC paths (<c>file://server/share</c>) — only opt in for trusted, local-only callers.
    /// </remarks>
    public IReadOnlyList<string> AllowedSchemes { get; set; } = ["https"];

    /// <summary>
    /// When <c>true</c>, hosts resolving to RFC 1918 / CGNAT / 0.0.0.0/8 are accepted.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>
    /// When <c>true</c>, hosts resolving to 127.0.0.0/8 or <c>::1</c> are accepted.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AllowLoopback { get; set; }

    /// <summary>
    /// When <c>true</c>, IDN hostnames are normalized to Punycode before validation.
    /// Defaults to <c>true</c>. Set to <c>false</c> only if upstream code has already normalized.
    /// </summary>
    public bool AllowIdn { get; set; } = true;

    /// <summary>
    /// When non-empty, the host MUST match at least one pattern.
    /// Pattern syntax: exact host, leading <c>*.</c> (matches subdomains and apex), or single <c>*</c>.
    /// </summary>
    public IReadOnlyList<string> AllowedHostPatterns { get; set; } = [];

    /// <summary>Patterns that, when matched, force a <see cref="UrlSafetyViolationKind.HostPatternDenied"/>.</summary>
    public IReadOnlyList<string> DeniedHostPatterns { get; set; } = [];

    /// <summary>Maximum permitted URL length in characters. Defaults to <c>2048</c>.</summary>
    [Range(1, int.MaxValue)]
    public int MaxUrlLength { get; set; } = 2048;

    /// <summary>Hard timeout applied to <see cref="System.Net.Dns.GetHostAddressesAsync(string, System.Threading.CancellationToken)"/>.</summary>
    public TimeSpan DnsResolveTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When <c>true</c>, the <c>file</c> scheme is accepted (in addition to being listed in
    /// <see cref="AllowedSchemes"/>). Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth: listing <c>"file"</c> in <see cref="AllowedSchemes"/> is not enough on its own;
    /// callers must also flip this toggle. Configuration drift on a list is more likely than on a
    /// typed boolean. When enabled, hosts on <c>file://</c> URIs (Windows UNC paths) are still rejected
    /// to prevent NTLM-relay / SMB egress via <c>file://attacker/share</c>.
    /// </remarks>
    public bool AllowFileScheme { get; set; }
}

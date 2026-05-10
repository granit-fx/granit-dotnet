namespace Granit.Http.Security;

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
    /// Defaults to <c>true</c>.
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
    public int MaxUrlLength { get; set; } = 2048;

    /// <summary>Hard timeout applied to <see cref="System.Net.Dns.GetHostAddressesAsync(string, System.Threading.CancellationToken)"/>.</summary>
    public TimeSpan DnsResolveTimeout { get; set; } = TimeSpan.FromSeconds(1);
}

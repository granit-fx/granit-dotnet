namespace Granit.IpGeolocation.Options;

/// <summary>
/// Core options for IP geolocation, bound from the <c>"IpGeolocation"</c> configuration section.
/// Provider packages add their own sub-sections (e.g. <c>"IpGeolocation:MaxMind"</c>).
/// </summary>
public sealed class GranitIpGeolocationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "IpGeolocation";

    /// <summary>
    /// Ordered list of <see cref="IIpGeolocationProvider.ProviderName"/> values defining the fallback chain:
    /// the resolver tries each in turn and returns the first match. When empty, all registered providers are
    /// tried in registration order. Names not matching a registered provider are skipped with a warning.
    /// </summary>
    /// <remarks>
    /// Prefer listing the offline provider first (e.g. <c>["MaxMind", "IpApi"]</c>) so that the privacy-friendly
    /// local lookup wins and third-party API calls only happen on a miss.
    /// </remarks>
    public IList<string> ProviderOrder { get; set; } = [];

    /// <summary>
    /// Time-to-live for cached lookup results (including negative results). Defaults to 1 hour.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// When <c>false</c> (default), private, loopback, and link-local addresses short-circuit to <c>null</c>
    /// without contacting any provider. Enable only for testing against curated fixtures.
    /// </summary>
    public bool ResolvePrivateAddresses { get; set; }
}

namespace Granit.Geocoding.Options;

/// <summary>
/// Core options for forward geocoding, bound from the <c>"Geocoding"</c> configuration section.
/// Provider packages add their own sub-sections (e.g. <c>"Geocoding:Nominatim"</c>).
/// </summary>
public sealed class GranitGeocodingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Geocoding";

    /// <summary>
    /// Ordered list of <see cref="IGeocodingProvider.ProviderName"/> values defining the fallback chain: the engine
    /// tries each in turn and returns the first match. When empty, all registered providers are tried in
    /// registration order. Names not matching a registered provider are skipped with a warning. When non-empty this
    /// list acts as an allow-list: a registered provider whose name is absent from it is excluded entirely.
    /// </summary>
    public IList<string> ProviderOrder { get; set; } = [];

    /// <summary>
    /// Time-to-live for a cached <em>successful</em> geocode. Addresses map to stable coordinates, so this is long
    /// by default (90 days) to minimise repeat calls to rate-limited providers.
    /// </summary>
    public TimeSpan SuccessCacheDuration { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Time-to-live for a cached <em>negative</em> result (no provider matched). Short by default (1 day) so a
    /// transient miss — provider downtime, a temporarily unindexed address — is retried soon, while still absorbing
    /// repeated lookups of a genuinely unresolvable address.
    /// </summary>
    public TimeSpan FailureCacheDuration { get; set; } = TimeSpan.FromDays(1);
}

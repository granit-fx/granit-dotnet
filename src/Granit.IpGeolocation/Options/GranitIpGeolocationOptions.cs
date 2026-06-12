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
    /// When non-empty this list acts as an allow-list: a registered provider whose name is absent from it is
    /// excluded from the chain entirely.
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

    /// <summary>
    /// Optional secret used to key the SHA-256 cache-key hash (turning it into an HMAC). When set, cache keys
    /// can no longer be reversed to the originating IP by brute force.
    /// </summary>
    /// <remarks>
    /// Cache keys hash the IP so a raw address is never written to a shared cache (e.g. Redis), where keys —
    /// unlike values — are not encrypted. A plain SHA-256 over the 2³² IPv4 space is, however, fully
    /// enumerable offline, so an actor with a cache dump could still recover every looked-up IPv4 address.
    /// Providing a secret here (sourced from configuration/Vault, and stable across every instance sharing the
    /// cache) closes that gap. Leave <c>null</c> to keep the unkeyed SHA-256 behaviour.
    /// </remarks>
    public string? CacheKeySecret { get; set; }
}

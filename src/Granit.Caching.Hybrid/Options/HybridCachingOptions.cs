namespace Granit.Caching.Hybrid.Options;

/// <summary>
/// Configuration options for the HybridCache provider (L1+L2).
/// Section <c>"Cache:Hybrid"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class HybridCachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache:Hybrid";

    /// <summary>
    /// Expiration duration for the L1 (per-pod local memory) cache.
    /// Should be short to limit the stale-data window between Kubernetes pods.
    /// Recommended maximum: 60 seconds.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan LocalCacheExpiration { get; set; } = TimeSpan.FromSeconds(30);
}

namespace Granit.Caching.Options;

/// <summary>
/// Configuration options for the FusionCache provider.
/// Section <c>"Cache:FusionCache"</c> in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Default configuration provides production-ready resilience:
/// <list type="bullet">
///   <item>Fail-safe enabled — expired entries serve as fallback when the factory fails</item>
///   <item>Soft timeout (2 s) — returns stale data if the factory is slow</item>
///   <item>Eager refresh at 80 % of TTL — background refresh before expiration</item>
///   <item>Backplane via Redis pub/sub — cross-pod L1 cache invalidation in real-time</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FusionCachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache:FusionCache";

    /// <summary>
    /// Enables fail-safe: expired entries are served as temporary fallback when the factory
    /// fails or times out. Default: <c>true</c>.
    /// </summary>
    public bool FailSafeIsEnabled { get; set; } = true;

    /// <summary>
    /// Maximum duration to keep fail-safe entries available as fallback.
    /// Default: 2 hours.
    /// </summary>
    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Minimum interval between consecutive fail-safe activations for the same key.
    /// Prevents retry storms when the downstream is consistently failing.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan FailSafeThrottleDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// If the factory takes longer than this and a stale value exists,
    /// the stale value is returned immediately while the factory continues in the background.
    /// Default: 2 seconds.
    /// </summary>
    public TimeSpan FactorySoftTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Absolute maximum time allowed for a factory execution.
    /// After this, the factory is abandoned regardless of stale data availability.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan FactoryHardTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Fraction of the entry duration at which eager (background) refresh is triggered.
    /// For example, 0.8 means refresh is triggered when 80 % of the TTL has elapsed.
    /// Set to <c>0</c> to disable eager refresh.
    /// Default: 0.8 (80 %).
    /// </summary>
    public float EagerRefreshThreshold { get; set; } = 0.8f;

    /// <summary>
    /// Redis pub/sub channel prefix for backplane notifications.
    /// Default: <c>"granit:fc"</c>.
    /// </summary>
    public string BackplaneChannelPrefix { get; set; } = "granit:fc";
}

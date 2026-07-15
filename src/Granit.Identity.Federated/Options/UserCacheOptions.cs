namespace Granit.Identity.Federated.Options;

/// <summary>
/// Configuration options for the identity user cache.
/// Bind to the <c>Identity:Federated:UserCache</c> configuration section.
/// </summary>
public sealed class UserCacheOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:UserCache";

    /// <summary>
    /// Duration after which a cached user entry is considered stale and eligible for re-fetch
    /// from the identity provider. Default: 24 hours.
    /// </summary>
    public TimeSpan StalenessThreshold { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Enables automatic cache sync on authenticated HTTP requests using JWT claims.
    /// When enabled, the <c>UserCacheSyncMiddleware</c> (Granit.Identity.Federated.Endpoints) upserts the current user
    /// from claims on each request if the cached entry is stale or missing. Default: <c>true</c>.
    /// </summary>
    public bool EnableLoginTimeSync { get; set; } = true;

    /// <summary>
    /// Maximum number of stale entries to refresh per incremental sync batch.
    /// Used by <see cref="Internal.CachedUserLookupService.RefreshStaleAsync"/>. Default: 50.
    /// </summary>
    public int IncrementalSyncBatchSize { get; set; } = 50;
}

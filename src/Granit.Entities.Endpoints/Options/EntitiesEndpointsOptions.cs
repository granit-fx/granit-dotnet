namespace Granit.Entities.Endpoints.Options;

/// <summary>
/// Configuration for the entity-manifest endpoints exposed by
/// <c>MapGranitEntitiesEndpoints</c>.
/// </summary>
public sealed class EntitiesEndpointsOptions
{
    /// <summary>OpenAPI Scalar tag (Title Case) shown in the docs UI. Default <c>"Entities"</c>.</summary>
    public string TagName { get; set; } = "Entities";

    /// <summary>
    /// FusionCache TTL for the per-entity manifest. The cache key includes the
    /// resolved user permission hash and the request culture, so the entry is
    /// safe to share across requests with the same security context. Default 5 minutes.
    /// </summary>
    public TimeSpan ManifestCacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// FusionCache TTL for the discovery tree. Same keying rules as
    /// <see cref="ManifestCacheTtl"/>. Default 5 minutes.
    /// </summary>
    public TimeSpan DiscoveryCacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// FusionCache TTL for relation aggregates returned by
    /// <c>POST /relations/aggregates</c>. Counts and sums change frequently —
    /// keep this short. Default 30 seconds. Per-(sourceEntity, sourceId)
    /// invalidation tags are emitted so future event-driven evictions can
    /// burst-clear all relation aggregates for a row in one call.
    /// </summary>
    public TimeSpan RelationAggregatesCacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// FusionCache TTL for the calendar range-query endpoint
    /// (<c>GET /api/entities/{name}/calendar</c>). Calendar data is more volatile
    /// than the manifest (new events appear continuously), so the default keeps
    /// the entry short — 1 minute. The cache key includes the resolved user
    /// permission hash plus the From/To window, so entries are safe to share
    /// across requests with the same security context and the same window.
    /// </summary>
    public TimeSpan CalendarRangeCacheTtl { get; set; } = TimeSpan.FromMinutes(1);
}

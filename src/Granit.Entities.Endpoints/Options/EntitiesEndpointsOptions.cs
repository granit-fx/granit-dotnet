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
}

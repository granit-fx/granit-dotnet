namespace Granit.Http.OutputCaching.Eviction;

/// <summary>
/// Abstraction for tag-based output cache eviction.
/// Wraps <c>IOutputCacheStore.EvictByTagAsync</c> so domain code does not depend
/// on <c>Microsoft.AspNetCore.OutputCaching</c> internals.
/// </summary>
public interface IOutputCacheEvictionService
{
    /// <summary>
    /// Evicts all cached responses tagged with the given Granit module name.
    /// </summary>
    /// <param name="moduleName">The module tag (e.g. <c>"Workflow"</c>, <c>"BlobStorage"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EvictModuleCacheAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts all cached responses for a specific tenant.
    /// Uses the <c>tenant:{tenantId}</c> tag set by <c>TenantAwareOutputCachePolicy</c>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EvictTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts all cached responses matching an arbitrary tag.
    /// </summary>
    /// <param name="tag">The cache tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EvictByTagAsync(string tag, CancellationToken cancellationToken = default);
}

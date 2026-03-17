using Granit.Http.OutputCaching.Policies;
using Microsoft.AspNetCore.OutputCaching;

namespace Granit.Http.OutputCaching.Eviction;

/// <summary>
/// Default implementation of <see cref="IOutputCacheEvictionService"/> backed by
/// <see cref="IOutputCacheStore"/>.
/// </summary>
internal sealed class OutputCacheEvictionService(IOutputCacheStore store) : IOutputCacheEvictionService
{
    /// <inheritdoc/>
    public async Task EvictModuleCacheAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        await store.EvictByTagAsync(moduleName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task EvictTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await store.EvictByTagAsync(
            $"{TenantAwareOutputCachePolicy.TenantTagPrefix}{tenantId}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task EvictByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        await store.EvictByTagAsync(tag, cancellationToken).ConfigureAwait(false);
    }
}

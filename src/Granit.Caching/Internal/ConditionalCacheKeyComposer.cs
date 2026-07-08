using Granit.Caching.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Internal;

/// <summary>
/// Composes the physical storage key for <see cref="IConditionalCache"/> entries:
/// <c>{KeyPrefix}:cond:t:{tenantId:N|host}:{logicalKey}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the isolation applied to <c>IFusionCache</c> keys (tenant segment from
/// <c>TenantAwareFusionCache</c> + app namespace from <c>FusionCacheOptions.CacheKeyPrefix</c>)
/// so conditional entries carry the same app and tenant namespacing structurally,
/// instead of relying on every caller to remember it. The <c>cond</c> segment keeps the
/// conditional keyspace disjoint from FusionCache entries sharing the same prefix.
/// </para>
/// <para>
/// The tenant segment is read from <see cref="ICurrentTenant"/> at call time
/// (<c>AsyncLocal</c>-backed), never captured at construction — the composer is a
/// singleton shared across requests, same reasoning as <c>TenantAwareFusionCache</c>.
/// </para>
/// </remarks>
internal sealed class ConditionalCacheKeyComposer(
    IOptions<CachingOptions> options,
    ICurrentTenant currentTenant)
{
    private string TenantSegment => currentTenant.IsAvailable
        ? currentTenant.Id!.Value.ToString("N")
        : "host";

    /// <summary>Returns the fully namespaced storage key for a logical key.</summary>
    public string Compose(string key) =>
        $"{options.Value.KeyPrefix}:cond:t:{TenantSegment}:{key}";
}

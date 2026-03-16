using Granit.Caching.Extensions;
using Granit.Core.Modularity;

namespace Granit.Caching;

/// <summary>
/// Granit module for the distributed cache with a default Memory provider.
/// </summary>
/// <remarks>
/// This module configures the <see cref="ICacheService{TCacheItem}"/> cache abstraction with
/// <c>MemoryDistributedCache</c> as the provider. Ideal for development and testing.
/// <para>
/// For production, replace with:
/// <list type="bullet">
///   <item><c>GranitCachingRedisModule</c> — Redis (cross-pod consistency)</item>
///   <item><c>GranitCachingHybridModule</c> — L1+L2 (Kubernetes performance)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class GranitCachingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCaching();
}

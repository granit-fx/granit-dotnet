using Granit.Caching.Extensions;
using Granit.Core.Modularity;

namespace Granit.Caching;

/// <summary>
/// Granit module for cache configuration and encryption.
/// </summary>
/// <remarks>
/// Registers <see cref="Options.CachingOptions"/>, <see cref="Options.CacheEncryptionOptions"/>,
/// and a no-op <see cref="ICacheValueEncryptor"/> (replaced by <c>AesCacheValueEncryptor</c>
/// when the Redis module enables encryption).
/// <para>
/// For the production caching provider, add <c>GranitCachingFusionCacheModule</c>
/// which registers <c>IFusionCache</c> with L1+L2+backplane.
/// </para>
/// </remarks>
public sealed class GranitCachingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCaching();
}

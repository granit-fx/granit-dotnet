using Granit.Caching.StackExchangeRedis.Extensions;
using Granit.Caching.StackExchangeRedis.Options;
using Granit.Core.Modularity;
using Microsoft.Extensions.Configuration;

namespace Granit.Caching.StackExchangeRedis;

/// <summary>
/// Granit module for the Redis distributed cache provider.
/// Replaces the Memory provider registered by <c>GranitCachingModule</c>.
/// </summary>
/// <remarks>
/// This module depends on <c>GranitCachingModule</c>, which registers the
/// <see cref="Granit.Caching.ICacheService{TCacheItem}"/> abstraction and options.
/// <para>
/// Enabling AES-256 encryption:
/// <code>
/// // appsettings.json
/// {
///   "Cache": {
///     "EncryptValues": true,
///     "Encryption": { "Key": "base64-key-from-vault" },
///     "Redis": { "Configuration": "redis:6379", "InstanceName": "app:" }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitCachingRedisModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        RedisCachingOptions redisOpts = context.Configuration
            .GetSection(RedisCachingOptions.SectionName)
            .Get<RedisCachingOptions>() ?? new RedisCachingOptions();

        return redisOpts.IsEnabled;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCachingRedis();
}

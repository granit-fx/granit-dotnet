using System.Text.Json;
using Granit.Caching.Internal;
using Granit.Caching.MultiTenancy;
using Granit.Caching.Options;
using Granit.Diagnostics;
using Granit.Json;
using Granit.MultiTenancy;
using Granit.Timing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.Extensions;

/// <summary>
/// DI registration extensions for <c>Granit.Caching</c>.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers cache configuration, FusionCache (L1 in-memory), encryption, and OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><see cref="CachingOptions"/> bound from <c>"Cache"</c> configuration section</item>
    ///   <item><see cref="CacheEncryptionOptions"/> bound from <c>"Cache:Encryption"</c> section</item>
    ///   <item><see cref="FusionCachingOptions"/> bound from <c>"Cache:FusionCache"</c> section</item>
    ///   <item><see cref="ICacheValueEncryptor"/> → <see cref="NullCacheValueEncryptor"/> (no-op by default)</item>
    ///   <item><c>IFusionCache</c> with L1 in-memory cache, fail-safe, factory timeouts, eager refresh</item>
    ///   <item><see cref="EncryptingFusionCacheSerializer"/> wrapping SystemTextJson for L2 encryption</item>
    /// </list>
    /// <para>
    /// For L2 Redis + backplane, add <c>Granit.Caching.StackExchangeRedis</c> which upgrades
    /// the FusionCache instance with <c>WithRegisteredDistributedCache()</c> and a Redis backplane.
    /// </para>
    /// <para>
    /// Tenant isolation caveat: <c>IFusionCache</c> keys are transparently prefixed per tenant,
    /// but <c>Clear()</c>/<c>ClearAsync()</c> are tenant-agnostic — they wipe entries of ALL
    /// tenants (FusionCache has no per-prefix clear). Prefer <c>RemoveByTag</c> with
    /// tenant-scoped tags for selective invalidation in multi-tenant hosts.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCaching(
        this IServiceCollection services)
    {
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // No-op encryptor by default (replaced by AesCacheValueEncryptor if EncryptValues=true)
        services.TryAddSingleton<ICacheValueEncryptor, NullCacheValueEncryptor>();

        // Shared key composition for IConditionalCache implementations: every key is
        // namespaced with {KeyPrefix}:cond:t:{tenant|host}: so conditional entries get the
        // same app + tenant isolation as IFusionCache keys (which go through
        // TenantAwareFusionCache + FusionCache's CacheKeyPrefix) instead of landing at the
        // Redis root where two apps sharing an instance could collide.
        services.TryAddSingleton<ConditionalCacheKeyComposer>();

        // In-memory conditional cache by default (replaced by RedisConditionalCache via Granit.Caching.StackExchangeRedis)
        services.TryAddSingleton<IConditionalCache, Internal.InMemoryConditionalCache>();

        // IDistributedCache (memory) for local development — replaced by Redis in production
        services.AddDistributedMemoryCache();

        // IClock for time-based operations
        services.AddGranitTiming();

        // FusionCachingOptions (fail-safe, timeouts, eager refresh, backplane prefix)
        services
            .AddOptions<FusionCachingOptions>()
            .BindConfiguration(FusionCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register FusionCache with L1 in-memory + SystemTextJson serialization
        services.AddFusionCache()
            .WithSystemTextJsonSerializer();

        // FusionCache emits its OTel signals natively from the core package, but under its own
        // namespace — the "Granit.*" wildcard and Granit source registrations never subscribe
        // them, so without these lines no cache metric or trace is exported at all. Name-based
        // registration (rather than referencing ZiggyCreatures.FusionCache.OpenTelemetry and
        // Granit.Observability) keeps the heavy OTel exporter stack out of this package's
        // dependency graph. Levels mirror the companion package defaults: traces for the
        // top-level + distributed (L2) operations, metrics for the top-level cache only —
        // per-level meters (memory/distributed/backplane) are too chatty for a default.
        GranitActivitySourceRegistry.Register(FusionCacheDiagnostics.ActivitySourceName);
        GranitActivitySourceRegistry.Register(FusionCacheDiagnostics.ActivitySourceNameDistributedLevel);
        GranitMeterRegistry.Register(FusionCacheDiagnostics.MeterName);

        // Deferred configuration: resolve options at runtime to configure FusionCache defaults
        services.AddOptions<FusionCacheOptions>()
            .Configure<IOptions<CachingOptions>, IOptions<FusionCachingOptions>>(
                (fc, cachingOpts, fusionOpts) =>
                {
                    fc.DefaultEntryOptions = new FusionCacheEntryOptions
                    {
                        Duration = cachingOpts.Value.DefaultAbsoluteExpirationRelativeToNow
                            ?? TimeSpan.FromHours(1),
                        IsFailSafeEnabled = fusionOpts.Value.FailSafeIsEnabled,
                        FailSafeMaxDuration = fusionOpts.Value.FailSafeMaxDuration,
                        FailSafeThrottleDuration = fusionOpts.Value.FailSafeThrottleDuration,
                        FactorySoftTimeout = fusionOpts.Value.FactorySoftTimeout,
                        FactoryHardTimeout = fusionOpts.Value.FactoryHardTimeout,
                        EagerRefreshThreshold = fusionOpts.Value.EagerRefreshThreshold,
                    };

                    fc.CacheKeyPrefix = cachingOpts.Value.KeyPrefix + ":";
                    fc.BackplaneChannelPrefix = fusionOpts.Value.BackplaneChannelPrefix;
                    fc.EnableAutoRecovery = true;
                });

        // Replace the serializer with the encrypting decorator (per-type via CacheEncryptionResolver).
        // The L2 serializer uses the same canonical Granit JSON converters as the HTTP pipeline so
        // domain value objects (SingleValueObject<T>, enums) round-trip through L2 identically — a
        // cached *Response/projection carrying e.g. an AbsoluteUrl deserialises correctly. The
        // host-supplied JsonOptions are copied (never mutated) before the converters are applied.
        services.AddSingleton<ZiggyCreatures.Caching.Fusion.Serialization.IFusionCacheSerializer>(sp =>
        {
            CachingOptions cachingOpts = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
            ICacheValueEncryptor encryptor = sp.GetRequiredService<ICacheValueEncryptor>();
            JsonSerializerOptions jsonOptions = cachingOpts.JsonOptions is null
                ? new JsonSerializerOptions()
                : new JsonSerializerOptions(cachingOpts.JsonOptions);
            jsonOptions.AddGranitJsonConverters();
            var jsonSerializer = new FusionCacheSystemTextJsonSerializer(jsonOptions);
            return new EncryptingFusionCacheSerializer(jsonSerializer, encryptor, cachingOpts);
        });

        // Ensure ICurrentTenant is available for the tenant-aware decorator.
        // In full app startup, Granit base module already registers NullTenantContext;
        // this TryAdd is a fallback for standalone AddGranitCaching() usage (tests, tooling).
        services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);

        // Tenant-aware cache key isolation: move the raw IFusionCache singleton to a
        // keyed service and register the decorator as the default IFusionCache.
        // All key-based operations are automatically prefixed with t:{tenantId}: or t:host:.
        //
        // The decorator is a SINGLETON — ICurrentTenant is backed by AsyncLocal<T>,
        // so the tenant is read at call time (not at construction time). This avoids
        // breaking singletons that inject IFusionCache (Localization, BFF, OIDC, etc.).
        ServiceDescriptor? rawDescriptor = services.LastOrDefault(d =>
            d.ServiceType == typeof(IFusionCache) && d.Lifetime == ServiceLifetime.Singleton);

        if (rawDescriptor is not null)
        {
            services.Remove(rawDescriptor);

            // Re-register the original singleton under a keyed service
            if (rawDescriptor.ImplementationFactory is not null)
            {
                services.AddKeyedSingleton<IFusionCache>(
                    TenantAwareFusionCache.RawCacheKey,
                    (sp, _) => (IFusionCache)rawDescriptor.ImplementationFactory(sp));
            }
            else if (rawDescriptor.ImplementationType is not null)
            {
                services.AddKeyedSingleton(
                    typeof(IFusionCache),
                    TenantAwareFusionCache.RawCacheKey,
                    rawDescriptor.ImplementationType);
            }

            // Singleton decorator as the default IFusionCache
            services.AddSingleton<IFusionCache>(sp => new TenantAwareFusionCache(
                sp.GetRequiredKeyedService<IFusionCache>(TenantAwareFusionCache.RawCacheKey),
                sp.GetRequiredService<ICurrentTenant>()));
        }

        return services;
    }
}

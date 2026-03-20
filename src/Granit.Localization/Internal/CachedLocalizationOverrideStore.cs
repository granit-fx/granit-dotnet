using Granit.Core.MultiTenancy;
using Granit.Localization.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Localization.Internal;

/// <summary>
/// Caching decorator for <see cref="ILocalizationOverrideStoreReader"/> and <see cref="ILocalizationOverrideStoreWriter"/>.
/// </summary>
/// <remarks>
/// Wraps any inner store (typically EF Core, registered as keyed service <see cref="RawStoreKey"/>)
/// with an <see cref="IFusionCache"/> (L1 + L2 + backplane), making read access synchronous-safe
/// for use inside <c>IStringLocalizer</c>.
/// <para>
/// An <see cref="AsyncServiceScope"/> is created per DB operation so that the underlying store
/// (Scoped) is resolved with its full dependency graph, including <c>AuditedEntityInterceptor</c>
/// for ISO 27001 audit compliance on write operations.
/// </para>
/// <para>
/// Cache is invalidated on every write or delete.
/// Cache keys are scoped per tenant when <see cref="ICurrentTenant"/> is registered.
/// </para>
/// </remarks>
internal sealed class CachedLocalizationOverrideStore(
    IFusionCache cache,
    IOptions<LocalizationOverridesCacheOptions> options,
    IServiceScopeFactory scopeFactory,
    IServiceProvider serviceProvider) : ILocalizationOverrideStoreReader, ILocalizationOverrideStoreWriter
{
    private readonly LocalizationOverridesCacheOptions _options = options.Value;

    /// <summary>
    /// Keyed service key used to register the underlying (non-cached) store.
    /// The EF Core module registers <c>EfCoreLocalizationOverrideStore</c> under this key.
    /// </summary>
    internal const string RawStoreKey = "localization-override-raw";

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> GetOverridesAsync(
        string resourceName, string culture, CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(resourceName, culture);

        MaybeValue<IReadOnlyDictionary<string, string>> maybe = cache.TryGet<IReadOnlyDictionary<string, string>>(cacheKey, token: cancellationToken);
        if (maybe.HasValue)
        {
            return Task.FromResult(maybe.Value);
        }

        return LoadAndCacheAsync(cacheKey, resourceName, culture, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetOverrideAsync(
        string resourceName, string culture, string key, string value, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ILocalizationOverrideStoreWriter inner =
            scope.ServiceProvider.GetRequiredKeyedService<ILocalizationOverrideStoreWriter>(RawStoreKey);

        await inner.SetOverrideAsync(resourceName, culture, key, value, cancellationToken).ConfigureAwait(false);
        cache.Expire(BuildCacheKey(resourceName, culture), token: cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveOverrideAsync(
        string resourceName, string culture, string key, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ILocalizationOverrideStoreWriter inner =
            scope.ServiceProvider.GetRequiredKeyedService<ILocalizationOverrideStoreWriter>(RawStoreKey);

        await inner.RemoveOverrideAsync(resourceName, culture, key, cancellationToken).ConfigureAwait(false);
        cache.Expire(BuildCacheKey(resourceName, culture), token: cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadAndCacheAsync(
        string cacheKey, string resourceName, string culture, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ILocalizationOverrideStoreReader? inner =
            scope.ServiceProvider.GetKeyedService<ILocalizationOverrideStoreReader>(RawStoreKey);

        // No raw store registered (EF Core package not installed): fall back to empty overrides
        // so the localizer resolves translations from embedded JSON files transparently.
        IReadOnlyDictionary<string, string> overrides = inner is not null
            ? await inner.GetOverridesAsync(resourceName, culture, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        cache.Set(cacheKey, overrides, new FusionCacheEntryOptions { Duration = _options.CacheTtl }, token: cancellationToken);
        return overrides;
    }

    private string BuildCacheKey(string resourceName, string culture)
    {
        ICurrentTenant? currentTenant = serviceProvider.GetService<ICurrentTenant>();
        string tenantSegment = currentTenant?.IsAvailable == true
            ? currentTenant.Id!.Value.ToString()
            : "host";

        return $"localization:{tenantSegment}:{resourceName}:{culture}";
    }
}

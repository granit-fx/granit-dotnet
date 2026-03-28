using Granit.Diagnostics;
using Granit.Localization.Diagnostics;
using Granit.Localization.Internal;
using Granit.Localization.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Granit.Localization.Extensions;

/// <summary>
/// DI registration extensions for Granit localization.
/// </summary>
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Granit localization services (embedded JSON) and the in-memory cache layer
    /// for DB overrides.
    /// </summary>
    /// <remarks>
    /// The cache layer (<see cref="CachedLocalizationOverrideStore"/>) activates automatically
    /// when a keyed <see cref="ILocalizationOverrideStoreReader"/> is registered under
    /// <see cref="CachedLocalizationOverrideStore.RawStoreKey"/>, e.g. by
    /// <c>Granit.Localization.EntityFrameworkCore</c>.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional options configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitLocalization(
        this IServiceCollection services,
        Action<GranitLocalizationOptions>? configure = null)
    {
        services.TryAddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<CachedLocalizationOverrideStore>();
        services.TryAddSingleton<ILocalizationOverrideStoreReader>(sp => sp.GetRequiredService<CachedLocalizationOverrideStore>());
        services.TryAddSingleton<ILocalizationOverrideStoreWriter>(sp => sp.GetRequiredService<CachedLocalizationOverrideStore>());

        GranitActivitySourceRegistry.Register(LocalizationActivitySource.Name);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>
    /// Configures the DB override cache TTL.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Cache options configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureLocalizationOverridesCache(
        this IServiceCollection services,
        Action<LocalizationOverridesCacheOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}

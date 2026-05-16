using System.Reflection;
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
    /// Explicitly registers a localization resource marker type and its embedded JSON files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consolidates the boilerplate of <c>options.Resources.Add&lt;T&gt;(...).AddJson(...)</c>.
    /// Use this from every module that owns a <see cref="LocalizationResourceNameAttribute"/>-decorated
    /// marker type, instead of relying on <see cref="GranitLocalizationOptions.EnableAutoDiscovery"/>
    /// which races with assembly load order.
    /// </para>
    /// <para>
    /// Assumes the standard Granit convention for embedded JSON: files live at
    /// <c>Localization/{ResourceName}/{culture}.json</c> inside the resource's owning
    /// assembly, producing the embedded resource prefix
    /// <c>{AssemblyName}.Localization.{ResourceName}</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResource">Marker class decorated with <see cref="LocalizationResourceNameAttribute"/>.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TResource"/> is not decorated with
    /// <see cref="LocalizationResourceNameAttribute"/>.</exception>
    public static IServiceCollection AddLocalizationResource<TResource>(this IServiceCollection services)
        where TResource : class
    {
        Type resourceType = typeof(TResource);
        LocalizationResourceNameAttribute attr =
            resourceType.GetCustomAttribute<LocalizationResourceNameAttribute>()
            ?? throw new InvalidOperationException(
                $"Type '{resourceType.FullName}' must be decorated with [LocalizationResourceName] "
                + "to be registered via AddLocalizationResource<T>().");

        Assembly assembly = resourceType.Assembly;
        string assemblyName = assembly.GetName().Name
            ?? throw new InvalidOperationException(
                $"Assembly for '{resourceType.FullName}' has no name.");

        string embeddedResourcePrefix = $"{assemblyName}.Localization.{attr.Name}";

        services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<TResource>(attr.DefaultCulture)
                .AddJson(assembly, embeddedResourcePrefix);
        });

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

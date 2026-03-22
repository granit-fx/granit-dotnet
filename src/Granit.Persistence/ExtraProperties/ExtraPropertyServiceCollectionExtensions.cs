using Granit.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.ExtraProperties;

/// <summary>
/// DI registration extensions for the ExtraProperties infrastructure.
/// </summary>
public static class ExtraPropertyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ExtraProperties sync interceptor and mapping registry as singletons.
    /// Safe to call multiple times — uses <c>TryAdd</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExtraPropertyInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ExtraPropertyMappingRegistry>();
        services.TryAddSingleton<IExtraPropertyMappingRegistry>(
            sp => sp.GetRequiredService<ExtraPropertyMappingRegistry>());
        services.TryAddSingleton<ExtraPropertySyncInterceptor>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ExtraPropertyMappingRegistryInitializer>());

        return services;
    }

    /// <summary>
    /// Registers extra property mappings for <typeparamref name="TEntity"/> and populates
    /// the <see cref="IExtraPropertyMappingRegistry"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IHasExtraProperties"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the property mappings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExtraPropertyMappings<TEntity>(
        this IServiceCollection services,
        Action<ExtraPropertyMappingOptions<TEntity>> configure)
        where TEntity : class, IHasExtraProperties
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddExtraPropertyInfrastructure();

        var options = new ExtraPropertyMappingOptions<TEntity>();
        configure(options);

        if (options.Mappings.Count > 0)
        {
            // Populate the registry at registration time (before app starts)
            services.AddSingleton<IConfigureExtraPropertyRegistry>(
                new ConfigureExtraPropertyRegistry<TEntity>(options.Mappings));
        }

        return services;
    }

    /// <summary>
    /// Registers extra property mappings for <typeparamref name="TEntity"/> by extracting them
    /// from an <see cref="IOptions{TOptions}"/> at startup. Useful when mappings are declared
    /// in a module-specific options class (e.g., <c>GranitUserExtensionOptions</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IHasExtraProperties"/>.</typeparam>
    /// <typeparam name="TOptions">The options type that contains the property mappings.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="extractor">
    /// Function that extracts <see cref="ExtraPropertyMapping"/> instances from the options.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExtraPropertyMappingsFromOptions<TEntity, TOptions>(
        this IServiceCollection services,
        Func<TOptions, List<ExtraPropertyMapping>> extractor)
        where TEntity : class, IHasExtraProperties
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(extractor);

        services.AddExtraPropertyInfrastructure();

        services.AddSingleton<IConfigureExtraPropertyRegistry>(sp =>
        {
            TOptions opts = sp.GetRequiredService<IOptions<TOptions>>().Value;
            List<ExtraPropertyMapping> mappings = extractor(opts);
            return new ConfigureExtraPropertyRegistry<TEntity>(mappings);
        });

        return services;
    }

    /// <summary>
    /// Marker interface for deferred registry population. Resolved by the registry
    /// at first use via <see cref="ExtraPropertyMappingRegistryInitializer"/>.
    /// </summary>
    internal interface IConfigureExtraPropertyRegistry
    {
        void Configure(ExtraPropertyMappingRegistry registry);
    }

    private sealed class ConfigureExtraPropertyRegistry<TEntity>(
        List<ExtraPropertyMapping> mappings) : IConfigureExtraPropertyRegistry
        where TEntity : class, IHasExtraProperties
    {
        public void Configure(ExtraPropertyMappingRegistry registry) =>
            registry.Register(typeof(TEntity), mappings);
    }
}

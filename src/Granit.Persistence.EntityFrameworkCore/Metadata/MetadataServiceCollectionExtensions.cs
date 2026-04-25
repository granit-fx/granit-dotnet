using Granit.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Metadata;

/// <summary>
/// DI registration extensions for the Metadata infrastructure.
/// </summary>
public static class MetadataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Metadata sync interceptor and mapping registry as singletons.
    /// Safe to call multiple times — uses <c>TryAdd</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetadataInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<MetadataMappingRegistry>();
        services.TryAddSingleton<IMetadataMappingRegistry>(
            sp => sp.GetRequiredService<MetadataMappingRegistry>());
        services.TryAddSingleton<MetadataSyncInterceptor>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, MetadataMappingRegistryInitializer>());

        return services;
    }

    /// <summary>
    /// Registers extra property mappings for <typeparamref name="TEntity"/> and populates
    /// the <see cref="IMetadataMappingRegistry"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IHasMetadata"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the property mappings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetadataMappings<TEntity>(
        this IServiceCollection services,
        Action<MetadataMappingOptions<TEntity>> configure)
        where TEntity : class, IHasMetadata
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddMetadataInfrastructure();

        var options = new MetadataMappingOptions<TEntity>();
        configure(options);

        if (options.Mappings.Count > 0)
        {
            // Populate the registry at registration time (before app starts)
            services.AddSingleton<IConfigureMetadataRegistry>(
                new ConfigureMetadataRegistry<TEntity>(options.Mappings));
        }

        return services;
    }

    /// <summary>
    /// Registers extra property mappings for <typeparamref name="TEntity"/> by extracting them
    /// from an <see cref="IOptions{TOptions}"/> at startup. Useful when mappings are declared
    /// in a module-specific options class (e.g., <c>GranitUserExtensionOptions</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IHasMetadata"/>.</typeparam>
    /// <typeparam name="TOptions">The options type that contains the property mappings.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="extractor">
    /// Function that extracts <see cref="MetadataMapping"/> instances from the options.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetadataMappingsFromOptions<TEntity, TOptions>(
        this IServiceCollection services,
        Func<TOptions, List<MetadataMapping>> extractor)
        where TEntity : class, IHasMetadata
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(extractor);

        services.AddMetadataInfrastructure();

        services.AddSingleton<IConfigureMetadataRegistry>(sp =>
        {
            TOptions opts = sp.GetRequiredService<IOptions<TOptions>>().Value;
            List<MetadataMapping> mappings = extractor(opts);
            return new ConfigureMetadataRegistry<TEntity>(mappings);
        });

        return services;
    }

    /// <summary>
    /// Marker interface for deferred registry population. Resolved by the registry
    /// at first use via <see cref="MetadataMappingRegistryInitializer"/>.
    /// </summary>
    internal interface IConfigureMetadataRegistry
    {
        void Configure(MetadataMappingRegistry registry);
    }

    private sealed class ConfigureMetadataRegistry<TEntity>(
        List<MetadataMapping> mappings) : IConfigureMetadataRegistry
        where TEntity : class, IHasMetadata
    {
        public void Configure(MetadataMappingRegistry registry) =>
            registry.Register(typeof(TEntity), mappings);
    }
}

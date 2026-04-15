using Granit.Diagnostics;
using Granit.ReferenceData.Diagnostics;
using Granit.ReferenceData.Internal;
using Granit.ReferenceData.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.ReferenceData.Extensions;

/// <summary>
/// Extensions for configuring Granit.ReferenceData services in the DI container.
/// </summary>
public static class ReferenceDataServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ReferenceData module services: options binding and memory cache.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional delegate to customize <see cref="ReferenceDataOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitReferenceData(
        this IServiceCollection services,
        Action<ReferenceDataOptions>? configure = null)
    {
        services
            .AddOptions<ReferenceDataOptions>()
            .BindConfiguration(ReferenceDataOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddMemoryCache();

        // Ensure singleton registry + initializer exist (idempotent for multiple AddReferenceData calls)
        services.TryAddSingleton<ReferenceDataRegistry>();
        services.TryAddSingleton<ReferenceDataMetrics>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ReferenceDataRegistryInitializer>());

        GranitActivitySourceRegistry.Register(ReferenceDataActivitySource.Name);

        return services;
    }
}

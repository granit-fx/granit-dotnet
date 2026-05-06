using Granit.Diagnostics;
using Granit.Entities.Diagnostics;
using Granit.Entities.Internal;
using Granit.Entities.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Entities.Extensions;

/// <summary>
/// Host-side DI registration for the <c>Granit.Entities</c> runtime: the
/// <see cref="IEntityDefinitionRegistry"/>, the boot-time integrity check, and the
/// <see cref="EntitiesOptions"/> binder.
/// </summary>
public static class EntitiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the entity-definition registry + the boot-time integrity-check runner.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for <see cref="EntitiesOptions"/> (e.g. switch the integrity check to Warn or Off).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEntities(
        this IServiceCollection services,
        Action<EntitiesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Make the IServiceCollection itself resolvable at runtime — the integrity-check
        // runner walks it to enumerate registered service types. This is the simplest
        // reliable way to inspect DI registrations without a private-API workaround.
        services.TryAddSingleton(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<EntitiesOptions>();
        }

        services.TryAddSingleton<IEntityDefinitionRegistry, EntityDefinitionRegistry>();
        services.AddHostedService<IntegrityCheckRunner>();

        GranitActivitySourceRegistry.Register(EntityActivitySource.Name);

        return services;
    }
}

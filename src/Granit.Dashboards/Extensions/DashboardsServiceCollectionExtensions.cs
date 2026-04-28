using Granit.Dashboards.Internal;
using Granit.Dashboards.Internal.Templating;
using Granit.Dashboards.Templating;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Dashboards.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Dashboards</c> services and shipped
/// dashboard definitions.
/// </summary>
public static class DashboardsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IDashboardDefinitionRegistry"/> that surfaces every
    /// registered <see cref="DashboardDefinition"/> across loaded modules.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDashboards(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDashboardDefinitionRegistry, DashboardDefinitionRegistry>();
        services.TryAddSingleton<IVariableSubstituter, DefaultVariableSubstituter>();

        return services;
    }

    /// <summary>
    /// Registers a dashboard definition. Each definition is exposed as a singleton
    /// <see cref="IDashboardDefinitionDescriptor"/> picked up by the
    /// <see cref="IDashboardDefinitionRegistry"/> at composition time.
    /// </summary>
    /// <typeparam name="TDefinition">The dashboard definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDashboardDefinition<TDefinition>(this IServiceCollection services)
        where TDefinition : DashboardDefinition, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TDefinition>(_ => new TDefinition());
        services.AddSingleton<IDashboardDefinitionDescriptor>(sp => sp.GetRequiredService<TDefinition>());

        return services;
    }
}

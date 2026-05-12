using Microsoft.Extensions.DependencyInjection;

namespace Granit.Dashboards.Extensions;

/// <summary>
/// DI helpers that operate on the declarative dashboard contracts. Lives in
/// <c>Granit.Dashboards.Abstractions</c> so modules can register dashboard definitions
/// without pulling in the dashboards runtime.
/// </summary>
public static class DashboardsAbstractionsServiceCollectionExtensions
{
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

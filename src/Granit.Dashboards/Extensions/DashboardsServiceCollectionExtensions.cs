using Granit.Dashboards.Internal;
using Granit.Dashboards.Internal.Templating;
using Granit.Dashboards.Rendering;
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
        services.TryAddScoped<IDashboardRenderer, DashboardRenderer>();

        services.AddScoped<IWidgetInstanceRenderer, MarkdownWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, TextWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, ImageWidgetInstanceRenderer>();

        return services;
    }
}

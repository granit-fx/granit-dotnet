using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.PostGIS.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.PostGIS.Extensions;

/// <summary>
/// DI registration helpers for <c>Granit.Analytics.PostGIS</c>.
/// </summary>
public static class GranitAnalyticsPostGISServiceCollectionExtensions
{
    /// <summary>
    /// Registers the open-generic <see cref="IGeographyPointProjector{TEntity}"/>
    /// implementation. With this registration in place, the framework's
    /// <c>MapRunner&lt;TEntity&gt;</c> resolves a projector for every entity that
    /// has a <c>Granit.Analytics.QueryDefinition</c>, without per-entity wiring
    /// in the host. Entities that don't expose a <c>NetTopologySuite.Geometries.Point</c>
    /// column will get an <see cref="ArgumentException"/> at the first row of
    /// the first render — the contract is "if you wire <c>Geography</c> on a
    /// MapWidget, the entity must carry a Point property under the configured
    /// column name".
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsPostGIS(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(ServiceDescriptor.Scoped(
            typeof(IGeographyPointProjector<>),
            typeof(NtsGeographyPointProjector<>)));

        return services;
    }
}

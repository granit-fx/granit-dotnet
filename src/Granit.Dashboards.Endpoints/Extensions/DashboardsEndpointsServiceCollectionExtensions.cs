using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Dashboards.Endpoints.Extensions;

/// <summary>
/// DI registration for the Granit.Dashboards endpoint surface — wires the import
/// service, the projection helper, and any future write-side service.
/// </summary>
public static class DashboardsEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required by the Granit.Dashboards endpoints — the
    /// import service today, more services as later stories ship. Idempotent
    /// (TryAdd).
    /// </summary>
    public static IServiceCollection AddGranitDashboardsEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<DashboardImporter>();

        return services;
    }
}

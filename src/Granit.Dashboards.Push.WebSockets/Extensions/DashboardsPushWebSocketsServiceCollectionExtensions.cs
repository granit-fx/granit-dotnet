using Granit.Dashboards.Push.WebSockets.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Dashboards.Push.WebSockets.Extensions;

/// <summary>
/// DI registration for the WebSocket sibling. Producer-side services (hub,
/// sequence allocator, <see cref="IWidgetPushPublisher"/>) come from
/// <c>Granit.Dashboards.Push</c>'s <c>AddGranitDashboardsPush()</c>; this
/// extension only registers the WebSocket-specific options.
/// </summary>
public static class DashboardsPushWebSocketsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WebSocket transport options. Idempotent. Hosts must also
    /// call <c>AddGranitDashboardsPush()</c> (transitively pulled in via the
    /// project reference) so the hub is wired before
    /// <c>MapGranitDashboardsPushWebSockets()</c> tries to resolve it.
    /// </summary>
    public static IServiceCollection AddGranitDashboardsPushWebSockets(
        this IServiceCollection services,
        Action<DashboardsPushWebSocketsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DashboardsPushWebSocketsOptions>()
            .Configure(opt =>
            {
                configure?.Invoke(opt);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}

using Granit.Notifications.Endpoints.Endpoints;
using Granit.Notifications.Endpoints.Options;
using Granit.Notifications.WebPush;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Notifications.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit.Notifications REST endpoints.
/// </summary>
public static class NotificationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all Granit.Notifications REST endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="NotificationEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitNotifications(
        this IEndpointRouteBuilder endpoints,
        Action<NotificationEndpointsOptions>? configure = null)
    {
        NotificationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        group.MapInboxEndpoints();
        group.MapActivityFeedEndpoints();
        group.MapPreferenceEndpoints();
        group.MapSubscriptionEndpoints();
        group.MapEntityFollowerEndpoints();

        // Web Push subscription routes are mapped only when the Web Push channel is registered
        // (via AddGranitNotificationsPush). Without the channel there is no IPushSubscriptionWriter
        // to serve them, so mapping them unconditionally would surface a 500 instead of a clean 404.
        if (((IEndpointRouteBuilder)group).ServiceProvider.GetService<IPushSubscriptionWriter>() is not null)
        {
            group.MapWebPushSubscriptionEndpoints(options.WebPushTagName);
        }

        return endpoints;
    }
}

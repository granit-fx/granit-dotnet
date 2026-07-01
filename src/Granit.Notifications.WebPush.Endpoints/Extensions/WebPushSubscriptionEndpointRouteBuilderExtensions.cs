using Granit.Notifications.WebPush.Endpoints.Endpoints;
using Granit.Notifications.WebPush.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.WebPush.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the browser Web Push subscription endpoints.
/// </summary>
public static class WebPushSubscriptionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Web Push subscription endpoints (<c>POST</c>/<c>DELETE</c>
    /// <c>{prefix}/push/subscriptions</c>). Requires the Web Push channel to be registered via
    /// <c>AddGranitNotificationsPush()</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="WebPushEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitWebPushSubscriptions(
        this IEndpointRouteBuilder endpoints,
        Action<WebPushEndpointsOptions>? configure = null)
    {
        WebPushEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName)
            .MapWebPushSubscriptionEndpoints();

        return endpoints;
    }
}

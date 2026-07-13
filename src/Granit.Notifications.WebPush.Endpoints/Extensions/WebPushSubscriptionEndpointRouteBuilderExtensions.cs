using Granit.Notifications.WebPush.Endpoints.Endpoints;
using Granit.Notifications.WebPush.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WebPush.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the browser Web Push subscription endpoints.
/// </summary>
public static class WebPushSubscriptionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Web Push subscription endpoints (<c>POST</c>/<c>DELETE</c>
    /// <c>{prefix}/subscriptions</c>). Requires the Web Push channel to be registered via
    /// <c>AddGranitNotificationsWebPush()</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="WebPushEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitWebPushSubscriptions(
        this IEndpointRouteBuilder endpoints,
        Action<WebPushEndpointsOptions>? configure = null)
    {
        // Configuration-bound values first (WebPushEndpointsOptions.SectionName), then the delegate
        // override. IOptionsFactory creates a fresh instance — the shared IOptions singleton
        // is never mutated.
        WebPushEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptionsFactory<WebPushEndpointsOptions>>()?
            .Create(Microsoft.Extensions.Options.Options.DefaultName) ?? new();
        configure?.Invoke(options);

        endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName)
            .MapWebPushSubscriptionEndpoints();

        return endpoints;
    }
}

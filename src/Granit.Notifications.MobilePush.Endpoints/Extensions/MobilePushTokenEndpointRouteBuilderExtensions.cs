using Granit.Notifications.MobilePush.Endpoints.Endpoints;
using Granit.Notifications.MobilePush.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.MobilePush.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the mobile push device token endpoints.
/// </summary>
public static class MobilePushTokenEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the mobile push token management endpoints (<c>POST</c>/<c>DELETE</c>/<c>GET</c> under
    /// the configured prefix). Requires the mobile push channel to be registered via
    /// <c>AddGranitNotificationsMobilePush()</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="MobilePushEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitMobilePushTokens(
        this IEndpointRouteBuilder endpoints,
        Action<MobilePushEndpointsOptions>? configure = null)
    {
        MobilePushEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName)
            .MapMobilePushTokenEndpoints();

        return endpoints;
    }
}

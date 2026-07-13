using Granit.Notifications.Endpoints.Endpoints;
using Granit.Notifications.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit.Notifications REST endpoints.
/// </summary>
public static class NotificationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the core Granit.Notifications REST endpoints (inbox, activity feed, preferences,
    /// subscriptions, entity followers).
    /// </summary>
    /// <remarks>
    /// Channel-specific endpoints are opt-in and live in their own packages:
    /// <c>MapGranitWebPushSubscriptions()</c> (Granit.Notifications.WebPush.Endpoints) and
    /// <c>MapGranitMobilePushTokens()</c> (Granit.Notifications.MobilePush.Endpoints).
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="NotificationEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitNotifications(
        this IEndpointRouteBuilder endpoints,
        Action<NotificationEndpointsOptions>? configure = null)
    {
        // Configuration-bound values first (NotificationEndpointsOptions.SectionName), then the delegate
        // override. IOptionsFactory creates a fresh instance — the shared IOptions singleton
        // is never mutated.
        NotificationEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptionsFactory<NotificationEndpointsOptions>>()?
            .Create(Microsoft.Extensions.Options.Options.DefaultName) ?? new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        group.MapInboxEndpoints();
        group.MapActivityFeedEndpoints();
        group.MapPreferenceEndpoints();
        group.MapSubscriptionEndpoints();
        group.MapEntityFollowerEndpoints();

        return endpoints;
    }
}

using Granit.QueryEngine.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Endpoints;
using Granit.Webhooks.Endpoints.Options;
using Granit.Webhooks.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Webhooks.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering webhook administration endpoints.
/// </summary>
public static class WebhooksEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the webhook administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires the <c>Webhooks.Subscriptions.Read</c> permission on the route group.
    /// Individual endpoints may require additional permissions (e.g. <c>Webhooks.Subscriptions.Manage</c>).
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapWebhooksEndpoints();
    ///
    /// // With custom options:
    /// app.MapWebhooksEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/webhooks";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="WebhooksEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapWebhooksEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<WebhooksEndpointsOptions>? configure = null)
    {
        WebhooksEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Read);

        group.MapEventTypeEndpoints();
        group.MapReadEndpoints();
        group.MapWriteEndpoints();
        group.MapLifecycleEndpoints();
        group.MapOperationEndpoints();

        // Query endpoints for subscription list and delivery attempts.
        // Use a temporary scope because IWebhookQueryableProvider is Scoped
        // when EF Core persistence is registered and cannot be resolved from the root provider.
        bool hasQueryableProvider;
        using (IServiceScope scope = endpoints.ServiceProvider.CreateScope())
        {
            hasQueryableProvider = scope.ServiceProvider.GetService<IWebhookQueryableProvider>() is not null;
        }

        if (hasQueryableProvider)
        {
            group.MapQueryEndpoints<WebhookSubscription>(
                "subscriptions/query",
                sp => sp.GetRequiredService<IWebhookQueryableProvider>().GetSubscriptions());

            group.MapQueryEndpoints<WebhookDeliveryAttempt>(
                "deliveries/query",
                sp => sp.GetRequiredService<IWebhookQueryableProvider>().GetDeliveryAttempts());
        }

        return group;
    }
}

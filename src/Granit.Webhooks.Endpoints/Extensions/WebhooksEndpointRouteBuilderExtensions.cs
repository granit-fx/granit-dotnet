using Granit.Querying.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Endpoints;
using Granit.Webhooks.Endpoints.Internal;
using Granit.Webhooks.Endpoints.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// Registers the <c>Webhooks.Subscriptions.Manage</c> authorization policy requiring
    /// the role configured via <see cref="WebhooksEndpointsOptions.RequiredRole"/>.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapWebhooksEndpoints();
    ///
    /// // With custom options:
    /// app.MapWebhooksEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/webhooks";
    ///     opts.RequiredRole = "ops-team";
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

        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        authOptions.Value.AddPolicy(
            WebhooksAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(WebhooksAuthorizationPolicy.PolicyName);

        group.MapReadEndpoints();
        group.MapWriteEndpoints();
        group.MapLifecycleEndpoints();
        group.MapOperationEndpoints();

        // Query endpoints for subscription list and delivery attempts
        IWebhookQueryableProvider? provider = endpoints.ServiceProvider.GetService<IWebhookQueryableProvider>();
        if (provider is not null)
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

using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Endpoints.Endpoints;
using Granit.Subscriptions.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Subscriptions.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering subscription administration endpoints.
/// </summary>
public static class SubscriptionsEndpointRouteBuilderExtensions
{
    /// <summary>Maps the subscription administration endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SubscriptionsEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitSubscriptions(
        this IEndpointRouteBuilder endpoints,
        Action<SubscriptionsEndpointsOptions>? configure = null)
    {
        SubscriptionsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        RouteGroupBuilder plansGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.PlansTagName);
        plansGroup.MapPlanReadEndpoints();
        plansGroup.MapPlanWriteEndpoints();

        RouteGroupBuilder pricesGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.PricesTagName);
        pricesGroup.MapPriceVersioningEndpoints();

        RouteGroupBuilder subscriptionsGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.SubscriptionsTagName);
        subscriptionsGroup.MapSubscriptionEndpoints();

        // Query engine endpoint — paginated, filterable list.
        // When no tenant context is active, the IQueryableSource disables the
        // multi-tenant filter so host admin sees all subscriptions cross-tenant.
        subscriptionsGroup.MapGranitGroup("subscriptions").MapGranitQuery<Subscription>();

        RouteGroupBuilder seatsGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.SeatsTagName);
        seatsGroup.MapSeatEndpoints();

        return group;
    }
}

using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Endpoints.Endpoints;
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
    public static RouteGroupBuilder MapGranitSubscriptions(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("subscriptions")
            .WithTags("Subscriptions");

        group.MapPlanReadEndpoints();
        group.MapPlanWriteEndpoints();
        group.MapPriceVersioningEndpoints();
        group.MapSubscriptionEndpoints();
        group.MapSeatEndpoints();

        // Query engine endpoint — paginated, filterable list.
        // When no tenant context is active, the IQueryableSource disables the
        // multi-tenant filter so host admin sees all subscriptions cross-tenant.
        group.MapGranitGroup("subscriptions").MapGranitQuery<Subscription>();

        return group;
    }
}

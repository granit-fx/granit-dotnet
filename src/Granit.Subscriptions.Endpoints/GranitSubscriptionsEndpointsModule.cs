using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Endpoints.Queries;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints;

/// <summary>
/// Granit module for subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitSubscriptionsModule),
    typeof(GranitValidationModule))]
public sealed class GranitSubscriptionsEndpointsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddQueryDefinition<Subscription, SubscriptionQueryDefinition>();
    }
}

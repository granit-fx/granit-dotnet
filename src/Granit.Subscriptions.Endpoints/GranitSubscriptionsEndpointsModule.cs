using Granit.Authorization;
using Granit.Entities;
using Granit.Entities.Relations;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Subscriptions.Endpoints.Relations;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints;

/// <summary>
/// Granit module for subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitSubscriptionsModule),
    typeof(GranitValidationModule))]
public sealed class GranitSubscriptionsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        // Phase 1.F-β cobaye: graft the "subscriptions" smart-button relation onto Party.
        context.Services.AddEntityRelationContribution<SubscriptionsOnPartyRelationContribution>();
}

using Granit.Authorization;
using Granit.CustomerBalance.Endpoints.Relations;
using Granit.Entities;
using Granit.Entities.Relations;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;

namespace Granit.CustomerBalance.Endpoints;

/// <summary>Granit module for customer balance HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCustomerBalanceModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitCustomerBalanceEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        // Phase 1.F-β cobaye: graft the "balance" smart-button relation onto Party.
        context.Services.AddEntityRelationContribution<BalanceOnPartyRelationContribution>();
}

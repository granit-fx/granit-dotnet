using Granit.Authorization;
using Granit.Entities;
using Granit.Entities.Relations;
using Granit.Modularity;
using Granit.Payments.Endpoints.Relations;
using Granit.RateLimiting;
using Granit.Validation;

namespace Granit.Payments.Endpoints;

/// <summary>Granit module for payment HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitPaymentsModule),
    typeof(GranitRateLimitingModule),
    typeof(GranitValidationModule))]
public sealed class GranitPaymentsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        // Phase 1.F-β cobaye: graft the "payments" smart-button relation onto Party.
        context.Services.AddEntityRelationContribution<PaymentsOnPartyRelationContribution>();
}

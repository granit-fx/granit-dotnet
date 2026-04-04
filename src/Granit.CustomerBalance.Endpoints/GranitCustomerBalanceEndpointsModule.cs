using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;

namespace Granit.CustomerBalance.Endpoints;

/// <summary>Granit module for customer balance HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCustomerBalanceModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitCustomerBalanceEndpointsModule : GranitModule;

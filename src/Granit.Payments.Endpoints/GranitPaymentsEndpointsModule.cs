using Granit.Authorization;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Payments.Endpoints;

/// <summary>Granit module for payment HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitPaymentsModule),
    typeof(GranitValidationModule))]
public sealed class GranitPaymentsEndpointsModule : GranitModule;

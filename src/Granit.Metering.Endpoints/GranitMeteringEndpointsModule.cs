using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;

namespace Granit.Metering.Endpoints;

/// <summary>
/// Granit module for metering HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitMeteringModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitMeteringEndpointsModule : GranitModule;

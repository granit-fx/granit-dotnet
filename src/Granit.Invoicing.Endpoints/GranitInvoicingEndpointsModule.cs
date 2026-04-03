using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;

namespace Granit.Invoicing.Endpoints;

/// <summary>Granit module for invoicing administration HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitInvoicingModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitInvoicingEndpointsModule : GranitModule;

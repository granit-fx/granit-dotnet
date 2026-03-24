using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Diagnostics.Endpoints;

/// <summary>
/// Granit module for diagnostics monitoring HTTP endpoints.
/// Exposes an aggregated health status endpoint for admin dashboards.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDiagnosticsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitDiagnosticsEndpointsModule : GranitModule;

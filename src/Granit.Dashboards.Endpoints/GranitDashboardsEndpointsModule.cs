using Granit.Authorization;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Dashboards.Endpoints;

/// <summary>
/// Granit module for the Granit.Dashboards HTTP endpoints.
/// </summary>
/// <remarks>
/// Wires the read-only catalogue endpoint today; the import / CRUD endpoints
/// land in subsequent stories on top of the same module.
/// </remarks>
[DependsOn(
    typeof(GranitDashboardsModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitDashboardsEndpointsModule : GranitModule;

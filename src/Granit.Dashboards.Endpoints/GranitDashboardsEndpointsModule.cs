using Granit.Authorization;
using Granit.Dashboards.Endpoints.Extensions;
using Granit.Dashboards.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Dashboards.Endpoints;

/// <summary>
/// Granit module for the Granit.Dashboards HTTP endpoints.
/// </summary>
/// <remarks>
/// Wires the read-only catalogue endpoint and the import endpoint today; the
/// list / read / state-transition endpoints land in subsequent stories on top
/// of the same module.
/// </remarks>
[DependsOn(
    typeof(GranitDashboardsModule),
    typeof(GranitDashboardsEntityFrameworkCoreModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitDashboardsEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDashboardsEndpoints();
}

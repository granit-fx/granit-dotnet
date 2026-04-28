using Granit.Analytics.Endpoints.Extensions;
using Granit.Authorization;
using Granit.Caching;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.Endpoints;

/// <summary>
/// Granit module for Granit.Analytics HTTP endpoints. Wires the metric endpoint
/// service, the period resolver, and a non-generic <c>IMetricRunner</c> per registered
/// <c>MetricDefinition</c>.
/// </summary>
[DependsOn(
    typeof(GranitAnalyticsModule),
    typeof(EntityFrameworkCore.GranitAnalyticsEntityFrameworkCoreModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitCachingModule),
    typeof(GranitValidationModule))]
public sealed class GranitAnalyticsEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAnalyticsEndpoints();
}

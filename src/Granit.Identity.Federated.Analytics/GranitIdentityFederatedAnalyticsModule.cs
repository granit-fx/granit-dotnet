using Granit.Analytics;
using Granit.Analytics.Extensions;
using Granit.Identity.Federated.Analytics.Metrics;
using Granit.Identity.Federated.Domain;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Federated.Analytics;

/// <summary>
/// Granit module for the Federated Identity analytics satellite. Registers the
/// MetricDefinitions observing the cached federated-identity pool (total and
/// enabled counts) so analytics hosts can surface them in dashboards and admin
/// pages.
/// </summary>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitIdentityFederatedAnalyticsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMetricDefinition<FederatedIdentity, int, FederatedIdentityCountMetricDefinition>();
        context.Services.AddMetricDefinition<FederatedIdentity, int, EnabledFederatedIdentityCountMetricDefinition>();
    }
}

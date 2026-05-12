using Granit.Analytics;
using Granit.Dashboards;
using Granit.Dashboards.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Webhooks.Dashboards;

/// <summary>
/// Granit module for the Webhooks dashboards satellite. Registers the
/// <see cref="WebhookReliabilityDashboardDefinition"/> so admin hosts surface it
/// through the dashboards endpoints.
/// </summary>
[DependsOn(
    typeof(GranitDashboardsAbstractionsModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitWebhooksDashboardsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddDashboardDefinition<WebhookReliabilityDashboardDefinition>();
    }
}

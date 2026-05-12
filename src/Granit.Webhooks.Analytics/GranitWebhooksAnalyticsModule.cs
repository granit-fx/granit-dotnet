using Granit.Analytics;
using Granit.Analytics.Extensions;
using Granit.Modularity;
using Granit.Webhooks.Analytics.Metrics;
using Granit.Webhooks.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Webhooks.Analytics;

/// <summary>
/// Granit module for the Webhooks analytics satellite. Registers the
/// MetricDefinitions observing webhook subscriptions and delivery attempts so
/// analytics hosts can surface them in dashboards and admin pages.
/// </summary>
[DependsOn(
    typeof(GranitWebhooksModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitWebhooksAnalyticsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMetricDefinition<WebhookSubscription, int, ActiveWebhookSubscriptionCountMetricDefinition>();
        context.Services.AddMetricDefinition<WebhookDeliveryAttempt, int, FailedWebhookDeliveryAttemptCountMetricDefinition>();
        context.Services.AddMetricDefinition<WebhookDeliveryAttempt, double, WebhookDeliverySuccessRateMetricDefinition>();
        context.Services.AddMetricDefinition<WebhookDeliveryAttempt, double, WebhookDeliveryLatencyAverageMetricDefinition>();
    }
}

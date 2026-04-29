using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.Webhooks.Dashboards;

/// <summary>
/// Webhook reliability dashboard — active subscriptions, success rate, average
/// delivery latency, plus a chart of failed deliveries over time. First-wave
/// reference for the <see cref="DashboardDefinition"/> pattern in the Webhooks
/// module.
/// </summary>
public sealed class WebhookReliabilityDashboardDefinition : DashboardDefinition
{
    /// <inheritdoc />
    public override string Name => "Granit.Webhooks.WebhookReliability";

    /// <inheritdoc />
    public override DashboardCategory Category => DashboardCategory.Operations;

    /// <inheritdoc />
    public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
    [
        new MarkdownWidgetDefinition(
            Slug: "banner",
            ContentLocalizationKey: "Widget:Granit.Webhooks.WebhookReliability.banner.Body",
            Position: 0),

        new KpiWidgetDefinition(
            Slug: "active-subscriptions",
            Datasource: Datasource.Metric("Granit.Webhooks.ActiveWebhookSubscriptionCountMetric"),
            Position: 1),

        new KpiWidgetDefinition(
            Slug: "success-rate",
            Datasource: Datasource.Metric("Granit.Webhooks.WebhookDeliverySuccessRateMetric"),
            Position: 2),

        new KpiWidgetDefinition(
            Slug: "latency-average",
            Datasource: Datasource.Metric("Granit.Webhooks.WebhookDeliveryLatencyAverageMetric"),
            Position: 3),

        new KpiWidgetDefinition(
            Slug: "failed-count",
            Datasource: Datasource.Metric("Granit.Webhooks.FailedWebhookDeliveryAttemptCountMetric"),
            Position: 4),

        new ChartWidgetDefinition(
            Slug: "failures-over-time",
            QueryName: "Granit.Webhooks.DeliveryAttemptsQuery",
            GroupBy: "OccurredAt",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Line,
            Position: 5),
    ];
}

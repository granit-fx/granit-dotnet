using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.Subscriptions.Dashboards;

/// <summary>
/// Subscriptions health dashboard — KPIs for active / trial / past-due /
/// dunning subscriptions plus a chart of cancellations over time. First-wave
/// reference for the <see cref="DashboardDefinition"/> pattern in the
/// Subscriptions module.
/// </summary>
public sealed class SubscriptionsHealthDashboardDefinition : DashboardDefinition
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.SubscriptionsHealth";

    /// <inheritdoc />
    public override DashboardCategory Category => DashboardCategory.Finance;

    /// <inheritdoc />
    public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
    [
        new MarkdownWidgetDefinition(
            Slug: "banner",
            ContentLocalizationKey: "Widget:Granit.Subscriptions.SubscriptionsHealth.banner.Body",
            Position: 0),

        new KpiWidgetDefinition(
            Slug: "active-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.ActiveSubscriptionCountMetric"),
            Position: 1),

        new KpiWidgetDefinition(
            Slug: "trial-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.TrialSubscriptionCountMetric"),
            Position: 2),

        new KpiWidgetDefinition(
            Slug: "past-due-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.PastDueSubscriptionCountMetric"),
            Position: 3),

        new KpiWidgetDefinition(
            Slug: "dunning-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.DunningSubscriptionCountMetric"),
            Position: 4),

        new ChartWidgetDefinition(
            Slug: "cancellations-over-time",
            QueryName: "Granit.Subscriptions.SubscriptionsQuery",
            GroupBy: "CancelledAt",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Line,
            Position: 5),
    ];
}

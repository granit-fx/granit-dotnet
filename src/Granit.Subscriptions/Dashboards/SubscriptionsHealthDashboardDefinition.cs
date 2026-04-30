using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.Subscriptions.Dashboards;

/// <summary>
/// Subscriptions health dashboard — top-row SaaS canonical measures
/// (MRR / ARR), then operational status KPIs (active / trial / past-due /
/// dunning), then a chart of cancellations over time. First-wave reference
/// for the <see cref="DashboardDefinition"/> pattern in the Subscriptions
/// module.
/// </summary>
public sealed class SubscriptionsHealthDashboardDefinition : DashboardDefinition
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.SubscriptionsHealth";

    /// <inheritdoc />
    public override DashboardCategory Category => DashboardCategory.Finance;

    /// <inheritdoc />
    /// <remarks>
    /// 1.1.0 — added the MRR + ARR widgets at the top of the grid (existing
    /// widgets shifted down). Tenants that imported v1.0.0 will see the
    /// drift surfaced through ADR-038 §3 once that detection lands; for now
    /// the gap is acceptable since the dashboard is shipped first-wave and
    /// few hosts are running off it in production.
    /// </remarks>
    public override string Version => "1.1.0";

    /// <inheritdoc />
    public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
    [
        new MarkdownWidgetDefinition(
            Slug: "banner",
            ContentLocalizationKey: "Widget:Granit.Subscriptions.SubscriptionsHealth.banner.Body",
            Position: 0),

        new KpiWidgetDefinition(
            Slug: "mrr",
            Datasource: Datasource.Metric("Granit.Subscriptions.MonthlyRecurringRevenueMetric"),
            Position: 1),

        new KpiWidgetDefinition(
            Slug: "arr",
            Datasource: Datasource.Metric("Granit.Subscriptions.AnnualRecurringRevenueMetric"),
            Position: 2),

        new KpiWidgetDefinition(
            Slug: "active-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.ActiveSubscriptionCountMetric"),
            Position: 3),

        new KpiWidgetDefinition(
            Slug: "trial-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.TrialSubscriptionCountMetric"),
            Position: 4),

        new KpiWidgetDefinition(
            Slug: "past-due-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.PastDueSubscriptionCountMetric"),
            Position: 5),

        new KpiWidgetDefinition(
            Slug: "dunning-count",
            Datasource: Datasource.Metric("Granit.Subscriptions.DunningSubscriptionCountMetric"),
            Position: 6),

        new ChartWidgetDefinition(
            Slug: "cancellations-over-time",
            QueryName: "Granit.Subscriptions.SubscriptionsQuery",
            GroupBy: "CancelledAt",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Line,
            Position: 7),
    ];
}

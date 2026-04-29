using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.Invoicing.Dashboards;

/// <summary>
/// Finance overview dashboard — first-wave reference shipped to demonstrate the
/// <see cref="DashboardDefinition"/> pattern. Six headline cash-flow KPIs
/// (unpaid / overdue / paid totals, plus the credit-note adjustment), a
/// monthly chart of issued volume, and a markdown banner above the grid.
/// Tenant admins import via
/// <c>POST /dashboards/from-definition/Granit.Invoicing.FinanceOverview</c>.
/// </summary>
public sealed class InvoicingFinanceOverviewDashboardDefinition : DashboardDefinition
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.FinanceOverview";

    /// <inheritdoc />
    public override DashboardCategory Category => DashboardCategory.Finance;

    /// <inheritdoc />
    public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
    [
        new MarkdownWidgetDefinition(
            Slug: "banner",
            ContentLocalizationKey: "Widget:Granit.Invoicing.FinanceOverview.banner.Body",
            Position: 0),

        new KpiWidgetDefinition(
            Slug: "unpaid-count",
            Datasource: Datasource.Metric("Granit.Invoicing.UnpaidInvoiceCountMetric"),
            Position: 1),

        new KpiWidgetDefinition(
            Slug: "unpaid-total",
            Datasource: Datasource.Metric("Granit.Invoicing.UnpaidInvoiceTotalMetric"),
            Position: 2),

        new KpiWidgetDefinition(
            Slug: "overdue-total",
            Datasource: Datasource.Metric("Granit.Invoicing.OverdueInvoiceTotalMetric"),
            Position: 3),

        new KpiWidgetDefinition(
            Slug: "paid-total",
            Datasource: Datasource.Metric("Granit.Invoicing.PaidInvoiceTotalMetric"),
            Position: 4),

        new KpiWidgetDefinition(
            Slug: "credit-note-total",
            Datasource: Datasource.Metric("Granit.Invoicing.CreditNoteTotalMetric"),
            Position: 5),

        new ChartWidgetDefinition(
            Slug: "issued-by-month",
            QueryName: "Granit.Invoicing.InvoiceQuery",
            GroupBy: "IssuedAt",
            Aggregation: AggregateFunction.Sum,
            Field: "Total",
            ChartType: ChartType.Bar,
            Position: 6),
    ];
}

using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

/// <summary>
/// Locks the propagation of <see cref="WidgetDefinition.Actions"/> through the four
/// data-bound analytics widgets. Default = <c>null</c>; explicit value travels
/// through the base record correctly.
/// </summary>
public sealed class AnalyticsWidgetActionsTests
{
    private static readonly WidgetAction[] OneClickAction =
    [
        new(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/invoicing?status=unpaid"),
    ];

    [Fact]
    public void Kpi_DefaultsToNoActions()
        => new KpiWidgetDefinition("K", Datasource.Metric("M"), Position: 0).Actions.ShouldBeNull();

    [Fact]
    public void Kpi_AcceptsActions()
    {
        KpiWidgetDefinition widget = new(
            "UnpaidCount", Datasource.Metric("Sample.Metric"), Position: 0, Actions: OneClickAction);

        widget.Actions.ShouldBe(OneClickAction);
    }

    [Fact]
    public void Chart_AcceptsSeriesClickAction()
    {
        WidgetAction[] actions =
        [
            new(WidgetActionTrigger.SeriesClick, WidgetActionKind.OpenDetail, "Drawer",
                Params: new Dictionary<string, string> { ["seriesValue"] = "${series.name}" }),
        ];

        ChartWidgetDefinition widget = new(
            "RevenueByMonth", "Sample.Q", "g", AggregateFunction.Sum, "f", ChartType.Bar,
            Position: 0, Actions: actions);

        widget.Actions.ShouldBe(actions);
        widget.Actions![0].Trigger.ShouldBe(WidgetActionTrigger.SeriesClick);
        widget.Actions[0].Params!["seriesValue"].ShouldBe("${series.name}");
    }

    [Fact]
    public void Table_AcceptsRowClickAction()
    {
        WidgetAction[] actions =
        [
            new(WidgetActionTrigger.RowClick, WidgetActionKind.OpenDetail, "InvoiceDrawer",
                Params: new Dictionary<string, string> { ["invoiceId"] = "${row.id}" }),
        ];

        TableWidgetDefinition widget = new(
            "RecentInvoices", "Sample.Q", null, 25, Position: 0, Actions: actions);

        widget.Actions.ShouldBe(actions);
    }

    [Fact]
    public void Pivot_AcceptsActions()
    {
        WidgetAction[] actions =
        [
            new(WidgetActionTrigger.Click, WidgetActionKind.ExportData, "Granit.Invoicing.InvoiceExport"),
        ];

        PivotWidgetDefinition widget = new(
            "InvoicesByCustomerByMonth", "Sample.Q", ["r"], ["c"], null, AggregateFunction.Count,
            Position: 0, Actions: actions);

        widget.Actions.ShouldBe(actions);
        widget.Actions![0].Kind.ShouldBe(WidgetActionKind.ExportData);
    }
}

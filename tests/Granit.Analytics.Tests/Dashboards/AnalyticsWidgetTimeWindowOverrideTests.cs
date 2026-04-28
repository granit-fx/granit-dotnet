using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

/// <summary>
/// Locks the per-widget <see cref="WidgetDefinition.TimeWindowOverride"/> field on the
/// four data-bound analytics widgets shipped by <c>Granit.Analytics</c>. The override
/// is opt-in — defaults to <c>null</c> so widgets honour the dashboard-wide
/// <c>DefaultTimeWindow</c>.
/// </summary>
public sealed class AnalyticsWidgetTimeWindowOverrideTests
{
    [Fact]
    public void Kpi_AcceptsTimeWindowOverride()
    {
        KpiWidgetDefinition widget = new(
            "YtdKpi", Datasource.Metric("Sample.Metric"), Position: 0,
            TimeWindowOverride: DashboardTimeWindow.Ytd);

        widget.TimeWindowOverride.ShouldBe(DashboardTimeWindow.Ytd);
    }

    [Fact]
    public void Chart_AcceptsTimeWindowOverride()
    {
        ChartWidgetDefinition widget = new(
            "Hist", "Sample.Q", "g", AggregateFunction.Sum, "f", ChartType.Line,
            Position: 0,
            TimeWindowOverride: DashboardTimeWindow.Last30Days);

        widget.TimeWindowOverride.ShouldBe(DashboardTimeWindow.Last30Days);
    }

    [Fact]
    public void Table_AcceptsTimeWindowOverride()
    {
        TableWidgetDefinition widget = new(
            "Table", "Sample.Q", null, 25, Position: 0,
            TimeWindowOverride: DashboardTimeWindow.Last7Days);

        widget.TimeWindowOverride.ShouldBe(DashboardTimeWindow.Last7Days);
    }

    [Fact]
    public void Pivot_AcceptsTimeWindowOverride()
    {
        PivotWidgetDefinition widget = new(
            "Pivot", "Sample.Q", ["r"], ["c"], null, AggregateFunction.Count,
            Position: 0,
            TimeWindowOverride: DashboardTimeWindow.Mtd);

        widget.TimeWindowOverride.ShouldBe(DashboardTimeWindow.Mtd);
    }

    [Fact]
    public void Kpi_DefaultsToNullOverride()
    {
        KpiWidgetDefinition widget = new("DefaultKpi", Datasource.Metric("Sample.Metric"), Position: 0);

        widget.TimeWindowOverride.ShouldBeNull();
    }
}

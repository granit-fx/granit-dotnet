using Granit.Analytics.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

/// <summary>
/// Smoke-level coverage of the five widget definition shapes shipped by B1. The
/// purpose is to lock the public API (constructor positional shape, default sizes,
/// nullable RequiredPermission), not to re-test record equality.
/// </summary>
public sealed class WidgetDefinitionTests
{
    [Fact]
    public void Kpi_DefaultsToSmallKpiSize()
    {
        KpiWidgetDefinition widget = new("Slug", "Sample.Metric", Position: 0);

        widget.Size.ShouldBe(WidgetSize.SmallKpi);
        widget.RequiredPermission.ShouldBeNull();
    }

    [Fact]
    public void Chart_DefaultsToStandardChartSize()
    {
        ChartWidgetDefinition widget = new(
            Slug: "RevenueByMonth",
            QueryName: "Sample.InvoiceQuery",
            GroupBy: "IssuedAtMonth",
            Aggregation: AggregateFunction.Sum,
            Field: "Total",
            ChartType: ChartType.Line,
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.StandardChart);
    }

    [Fact]
    public void Table_AllowsNullVisibleColumns_AndCustomPageSize()
    {
        TableWidgetDefinition widget = new(
            Slug: "RecentInvoices",
            QueryName: "Sample.InvoiceQuery",
            VisibleColumns: null,
            PageSize: 25,
            Position: 0);

        widget.VisibleColumns.ShouldBeNull();
        widget.PageSize.ShouldBe(25);
    }

    [Fact]
    public void Pivot_AllowsNullValueField_WhenAggregationIsCount()
    {
        PivotWidgetDefinition widget = new(
            Slug: "InvoicesByCustomerByMonth",
            QueryName: "Sample.InvoiceQuery",
            RowFields: ["CustomerName"],
            ColumnFields: ["IssuedAtMonth"],
            ValueField: null,
            ValueAggregation: AggregateFunction.Count,
            Position: 0);

        widget.ValueField.ShouldBeNull();
        widget.ValueAggregation.ShouldBe(AggregateFunction.Count);
    }

    [Fact]
    public void Markdown_DefaultsToFullWidthRow_AndIgnoresPermissions()
    {
        MarkdownWidgetDefinition widget = new(
            Slug: "Banner",
            ContentLocalizationKey: "Widget:Sample.Banner",
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.FullWidthRow);
        // Markdown is always visible — permission filtering does not apply.
        widget.RequiredPermission.ShouldBeNull();
    }

    [Fact]
    public void Widget_AllowsRequiredPermissionOverride()
    {
        KpiWidgetDefinition widget = new(
            Slug: "RestrictedKpi",
            MetricName: "Sample.Metric",
            Position: 0,
            RequiredPermission: "Custom.Composite.Read");

        widget.RequiredPermission.ShouldBe("Custom.Composite.Read");
    }
}

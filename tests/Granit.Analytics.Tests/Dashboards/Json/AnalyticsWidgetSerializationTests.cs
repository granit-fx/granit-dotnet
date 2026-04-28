using System.Text.Json;
using Granit.Analytics.Dashboards.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards.Json;

/// <summary>
/// Exercises the round-trip for the four analytics widgets registered via
/// <see cref="AnalyticsWidgetSerialization.AddAnalyticsWidgets"/> — the wire format
/// must surface the canonical short discriminators (<c>"kpi"</c>, <c>"chart"</c>,
/// <c>"table"</c>, <c>"pivot"</c>) and survive a serialize / deserialize cycle.
/// </summary>
public sealed class AnalyticsWidgetSerializationTests
{
    private static JsonSerializerOptions NewOptions()
    {
        JsonSerializerOptions options = new();
        options.AddAnalyticsWidgets();
        return options;
    }

    [Fact]
    public void Kpi_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new KpiWidgetDefinition(
            "UnpaidCount", Datasource.Metric("Granit.Invoicing.UnpaidInvoiceCountMetric"), Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"kpi\"");
        json.ShouldNotContain("$type");
    }

    [Fact]
    public void Chart_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new ChartWidgetDefinition(
            Slug: "RevenueByMonth",
            QueryName: "Granit.Invoicing.InvoiceQuery",
            GroupBy: "IssuedAtMonth",
            Aggregation: AggregateFunction.Sum,
            Field: "Total",
            ChartType: ChartType.Line,
            Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"chart\"");
    }

    [Fact]
    public void Table_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new TableWidgetDefinition(
            "RecentInvoices", "Granit.Invoicing.InvoiceQuery", VisibleColumns: null,
            PageSize: 25, Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"table\"");
    }

    [Fact]
    public void Pivot_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new PivotWidgetDefinition(
            "InvoicesByCustomerByMonth", "Granit.Invoicing.InvoiceQuery",
            RowFields: ["CustomerName"], ColumnFields: ["IssuedAtMonth"],
            ValueField: null, ValueAggregation: AggregateFunction.Count, Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"pivot\"");
    }

    [Fact]
    public void RoundTrip_PreservesAllFourAnalyticsWidgetTypes()
    {
        JsonSerializerOptions options = NewOptions();

        WidgetDefinition[] originals =
        [
            new KpiWidgetDefinition("Kpi", Datasource.Metric("M1"), Position: 0),
            new ChartWidgetDefinition("Chart", "Q1", "g", AggregateFunction.Sum, "f", ChartType.Bar, Position: 1),
            new TableWidgetDefinition("Table", "Q1", null, 25, Position: 2),
            new PivotWidgetDefinition("Pivot", "Q1", ["r"], ["c"], null, AggregateFunction.Count, Position: 3),
        ];

        foreach (WidgetDefinition original in originals)
        {
            string json = JsonSerializer.Serialize(original, options);
            WidgetDefinition? decoded = JsonSerializer.Deserialize<WidgetDefinition>(json, options);

            decoded.ShouldNotBeNull();
            decoded.GetType().ShouldBe(original.GetType());
        }
    }
}

using System.Text.Json;
using Granit.Analytics.Dashboards.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards.Json;

/// <summary>
/// Exercises the round-trip for the analytics widgets registered via
/// <see cref="AnalyticsWidgetSerialization.AddAnalyticsWidgets"/> — the wire format
/// must surface the canonical short discriminators (<c>"kpi"</c>, <c>"chart"</c>,
/// <c>"table"</c>, <c>"pivot"</c>, <c>"map"</c>) and survive a serialize /
/// deserialize cycle.
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
    public void Map_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new MapWidgetDefinition(
            Slug: "CustomerLocations",
            QueryName: "Granit.Parties.PartyQuery",
            PointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
            PopupColumns: ["DisplayName", "City"],
            Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"map\"");
        json.ShouldNotContain("$type");
    }

    [Fact]
    public void Map_PostGisVariant_SerializesWithGeographyColumn()
    {
        WidgetDefinition widget = new MapWidgetDefinition(
            Slug: "BranchOffices",
            QueryName: "Granit.Parties.OfficeQuery",
            PointSource: new MapPointSource.Geography("Location"),
            PopupColumns: ["DisplayName"],
            Position: 0);

        string json = JsonSerializer.Serialize(widget, NewOptions());

        json.ShouldContain("\"type\":\"map\"");
        json.ShouldContain("\"kind\":\"geography\"");
        json.ShouldContain("\"GeographyColumn\":\"Location\"");
    }

    [Fact]
    public void RoundTrip_PreservesAllAnalyticsWidgetTypes()
    {
        JsonSerializerOptions options = NewOptions();

        WidgetDefinition[] originals =
        [
            new KpiWidgetDefinition("Kpi", Datasource.Metric("M1"), Position: 0),
            new ChartWidgetDefinition("Chart", "Q1", "g", AggregateFunction.Sum, "f", ChartType.Bar, Position: 1),
            new TableWidgetDefinition("Table", "Q1", null, 25, Position: 2),
            new PivotWidgetDefinition("Pivot", "Q1", ["r"], ["c"], null, AggregateFunction.Count, Position: 3),
            new MapWidgetDefinition("Map", "Q1", new MapPointSource.LatLng("Lat", "Lng"), ["Name"], Position: 4),
        ];

        foreach (WidgetDefinition original in originals)
        {
            string json = JsonSerializer.Serialize(original, options);
            WidgetDefinition? decoded = JsonSerializer.Deserialize<WidgetDefinition>(json, options);

            decoded.ShouldNotBeNull();
            decoded.GetType().ShouldBe(original.GetType());
        }
    }

    [Fact]
    public void Map_RoundTrip_PreservesAllConfigurationFields()
    {
        JsonSerializerOptions options = NewOptions();

        MapWidgetDefinition original = new(
            Slug: "Customers",
            QueryName: "Granit.Parties.PartyQuery",
            PointSource: new MapPointSource.LatLng("Lat", "Lng"),
            PopupColumns: ["Name", "City"],
            Position: 0,
            DefaultZoom: 8,
            DefaultCenter: new MapCenter(50.85, 4.35),
            ClusterThreshold: 500,
            DetailRoute: "/customers/{id}",
            TileUrlTemplate: "https://tiles.example.com/{z}/{x}/{y}.png");

        string json = JsonSerializer.Serialize<WidgetDefinition>(original, options);
        WidgetDefinition? decoded = JsonSerializer.Deserialize<WidgetDefinition>(json, options);

        MapWidgetDefinition map = decoded.ShouldBeOfType<MapWidgetDefinition>();
        map.Slug.ShouldBe("Customers");
        map.QueryName.ShouldBe("Granit.Parties.PartyQuery");
        map.PointSource.ShouldBeOfType<MapPointSource.LatLng>();
        map.PopupColumns.ShouldBe(["Name", "City"]);
        map.DefaultZoom.ShouldBe(8);
        map.DefaultCenter.ShouldNotBeNull();
        map.DefaultCenter.Latitude.ShouldBe(50.85);
        map.DefaultCenter.Longitude.ShouldBe(4.35);
        map.ClusterThreshold.ShouldBe(500);
        map.DetailRoute.ShouldBe("/customers/{id}");
        map.TileUrlTemplate.ShouldBe("https://tiles.example.com/{z}/{x}/{y}.png");
    }
}

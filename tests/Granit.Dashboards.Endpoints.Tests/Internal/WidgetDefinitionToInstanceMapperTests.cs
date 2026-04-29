using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Locks the typed-widget-to-persisted-shape mapping used by the import endpoint.
/// Each kind must produce the right discriminator string + denormalised metric /
/// query references + a parseable ConfigJson payload.
/// </summary>
public sealed class WidgetDefinitionToInstanceMapperTests
{
    [Fact]
    public void Markdown_MapsToMarkdownTypeWithContentKey()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new MarkdownWidgetDefinition("Banner", "Widget:S.Banner", Position: 0));

        result.WidgetType.ShouldBe("Markdown");
        result.MetricName.ShouldBeNull();
        result.QueryName.ShouldBeNull();
        result.ConfigJson.ShouldContain("Widget:S.Banner");
    }

    [Fact]
    public void Image_MapsToImageTypeWithSourceAltAndFit()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new ImageWidgetDefinition("Logo", "https://cdn.example/logo.png", "Widget:S.Logo.Alt", Position: 0, Fit: ImageFit.Cover));

        result.WidgetType.ShouldBe("Image");
        result.ConfigJson.ShouldContain("https://cdn.example/logo.png");
        result.ConfigJson.ShouldContain("Cover");
    }

    [Fact]
    public void Text_MapsToTextTypeWithStyle()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new TextWidgetDefinition("Title", "Widget:S.Title", TextStyle.Heading, Position: 0));

        result.WidgetType.ShouldBe("Text");
        result.ConfigJson.ShouldContain("Heading");
    }

    [Fact]
    public void Kpi_WithMetricDatasource_ExtractsMetricName()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new KpiWidgetDefinition(
                    "UnpaidCount",
                    Datasource.Metric("Granit.Invoicing.UnpaidInvoiceCountMetric"),
                    Position: 0));

        result.WidgetType.ShouldBe("Kpi");
        result.MetricName.ShouldBe("Granit.Invoicing.UnpaidInvoiceCountMetric");
        result.QueryName.ShouldBeNull();
        // ConfigJson carries the full datasource via P1.1 polymorphism — discriminator "metric"
        result.ConfigJson.ShouldContain("\"kind\":\"metric\"");
    }

    [Fact]
    public void Kpi_WithQueryAggregateDatasource_ExtractsQueryName()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new KpiWidgetDefinition(
                    "RevenueAgg",
                    Datasource.QueryAggregate("Sample.Q", AggregateFunction.Sum, "Total"),
                    Position: 0));

        result.WidgetType.ShouldBe("Kpi");
        result.MetricName.ShouldBeNull();
        result.QueryName.ShouldBe("Sample.Q");
        result.ConfigJson.ShouldContain("\"kind\":\"query-aggregate\"");
    }

    [Fact]
    public void Kpi_WithTelemetryDatasource_HasNeitherDenormalisedRef()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new KpiWidgetDefinition(
                    "Temperature",
                    Datasource.Telemetry("currentDevice", "temperature"),
                    Position: 0));

        result.WidgetType.ShouldBe("Kpi");
        result.MetricName.ShouldBeNull();
        result.QueryName.ShouldBeNull();
        result.ConfigJson.ShouldContain("\"kind\":\"iot-telemetry\"");
    }

    [Fact]
    public void Chart_MapsQueryNameAndAggregation()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new ChartWidgetDefinition(
                    "RevenueByMonth",
                    "Sample.Q",
                    "IssuedAtMonth",
                    AggregateFunction.Sum,
                    "Total",
                    ChartType.Line,
                    Position: 0));

        result.WidgetType.ShouldBe("Chart");
        result.QueryName.ShouldBe("Sample.Q");
        result.ConfigJson.ShouldContain("Line");
        result.ConfigJson.ShouldContain("Sum");
    }

    [Fact]
    public void Table_MapsQueryNameAndPageSize()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new TableWidgetDefinition("Recent", "Sample.Q", null, 25, Position: 0));

        result.WidgetType.ShouldBe("Table");
        result.QueryName.ShouldBe("Sample.Q");
        result.ConfigJson.ShouldContain("25");
    }

    [Fact]
    public void Pivot_MapsRowsAndColumns()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new PivotWidgetDefinition(
                    "ByCustomerByMonth",
                    "Sample.Q",
                    ["customer"],
                    ["month"],
                    "amount",
                    AggregateFunction.Sum,
                    Position: 0));

        result.WidgetType.ShouldBe("Pivot");
        result.QueryName.ShouldBe("Sample.Q");
        result.ConfigJson.ShouldContain("customer");
        result.ConfigJson.ShouldContain("month");
    }

    [Fact]
    public void ConfigJson_IsValidJson_ForEveryKind()
    {
        WidgetDefinition[] widgets =
        [
            new MarkdownWidgetDefinition("M", "Widget:M", Position: 0),
            new ImageWidgetDefinition("I", "https://x", "Widget:I.Alt", Position: 1),
            new TextWidgetDefinition("T", "Widget:T", TextStyle.Body, Position: 2),
            new KpiWidgetDefinition("K", Datasource.Metric("M1"), Position: 3),
            new ChartWidgetDefinition("C", "Q1", "g", AggregateFunction.Count, null, ChartType.Bar, Position: 4),
            new TableWidgetDefinition("Tab", "Q1", null, 10, Position: 5),
            new PivotWidgetDefinition("P", "Q1", ["r"], ["c"], null, AggregateFunction.Count, Position: 6),
            new MapWidgetDefinition(
                "M2", "Q1",
                new MapPointSource.LatLng("Lat", "Lng"),
                PopupColumns: null,
                Position: 7),
        ];

        foreach (WidgetDefinition widget in widgets)
        {
            WidgetDefinitionToInstanceMapper.Mapping result = WidgetDefinitionToInstanceMapper.Map(widget);

            // Should not throw — every ConfigJson must be parseable JSON.
            using var doc = JsonDocument.Parse(result.ConfigJson);
            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        }
    }

    [Fact]
    public void Map_MapsToMapType_WithLatLngPointSource_PersistedAsKindKey()
    {
        // Persisted ConfigJson follows the same JSON polymorphism convention
        // as the renderer's MapConfig deserialiser — the discriminator field
        // is "kind" with values "lat-lng" / "geography" (kebab-case to mirror
        // MapPointSource.JsonPolymorphic attribute on the abstraction).
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new MapWidgetDefinition(
                    "Customers",
                    "Test.Customers",
                    new MapPointSource.LatLng("Latitude", "Longitude"),
                    PopupColumns: ["Name", "Country"],
                    Position: 0));

        result.WidgetType.ShouldBe("Map");
        result.QueryName.ShouldBe("Test.Customers");

        using var doc = JsonDocument.Parse(result.ConfigJson);
        JsonElement root = doc.RootElement;

        root.GetProperty("pointSource").GetProperty("kind").GetString().ShouldBe("lat-lng");
        root.GetProperty("pointSource").GetProperty("latitudeColumn").GetString().ShouldBe("Latitude");
        root.GetProperty("pointSource").GetProperty("longitudeColumn").GetString().ShouldBe("Longitude");
        root.GetProperty("popupColumns").EnumerateArray().Select(e => e.GetString()).ShouldBe(["Name", "Country"]);

        // DefaultLayerKind absent on the definition → null on the wire.
        root.GetProperty("defaultLayerKind").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Map_DefaultLayerKind_RoundTripsAsPascalCaseString()
    {
        // B7-3 (#1577) — the persisted ConfigJson MUST carry the layer kind
        // as a PascalCase string ("Satellite", not "SATELLITE", not "1") so
        // the renderer can deserialise it via JsonStringEnumConverter() into
        // MapTileLayerKind without a custom converter.
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new MapWidgetDefinition(
                    "Deliveries",
                    "Test.Deliveries",
                    new MapPointSource.LatLng("Lat", "Lng"),
                    PopupColumns: null,
                    Position: 0,
                    DefaultLayerKind: MapTileLayerKind.Satellite));

        using var doc = JsonDocument.Parse(result.ConfigJson);
        doc.RootElement.GetProperty("defaultLayerKind").GetString().ShouldBe("Satellite");
    }

    [Fact]
    public void Map_GeographyPointSource_PersistedWithGeographyDiscriminator()
    {
        WidgetDefinitionToInstanceMapper.Mapping result =
            WidgetDefinitionToInstanceMapper.Map(
                new MapWidgetDefinition(
                    "Branches",
                    "Test.Branches",
                    new MapPointSource.Geography("Location"),
                    PopupColumns: null,
                    Position: 0));

        using var doc = JsonDocument.Parse(result.ConfigJson);
        JsonElement pointSource = doc.RootElement.GetProperty("pointSource");
        pointSource.GetProperty("kind").GetString().ShouldBe("geography");
        pointSource.GetProperty("geographyColumn").GetString().ShouldBe("Location");
    }
}

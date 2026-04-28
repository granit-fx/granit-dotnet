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
        ];

        foreach (WidgetDefinition widget in widgets)
        {
            WidgetDefinitionToInstanceMapper.Mapping result = WidgetDefinitionToInstanceMapper.Map(widget);

            // Should not throw — every ConfigJson must be parseable JSON.
            using var doc = JsonDocument.Parse(result.ConfigJson);
            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        }
    }
}

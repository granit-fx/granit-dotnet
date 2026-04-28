using System.Text.Json;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape and JSON wire format of <see cref="Datasource"/> + the
/// three derived kinds (P2.2). Pinning the kebab discriminators is the
/// load-bearing assertion — the frontend's TypeScript discriminated union
/// mirrors them 1:1.
/// </summary>
public sealed class DatasourceTests
{
    [Fact]
    public void MetricDatasource_SerializesWithKebabDiscriminator()
    {
        Datasource ds = new MetricDatasource("Granit.Invoicing.UnpaidInvoiceCountMetric");
        string json = JsonSerializer.Serialize(ds);

        json.ShouldContain("\"kind\":\"metric\"");
        json.ShouldNotContain("$type");

        Datasource? decoded = JsonSerializer.Deserialize<Datasource>(json);
        decoded.ShouldBeOfType<MetricDatasource>();
        ((MetricDatasource)decoded!).MetricName.ShouldBe("Granit.Invoicing.UnpaidInvoiceCountMetric");
    }

    [Fact]
    public void QueryAggregateDatasource_SerializesWithFieldAndAggregation()
    {
        Datasource ds = new QueryAggregateDatasource(
            "Granit.Invoicing.InvoiceQuery", AggregateFunction.Sum, "Total");

        string json = JsonSerializer.Serialize(ds);
        json.ShouldContain("\"kind\":\"query-aggregate\"");

        Datasource? decoded = JsonSerializer.Deserialize<Datasource>(json);
        QueryAggregateDatasource typed = decoded.ShouldBeOfType<QueryAggregateDatasource>();
        typed.QueryName.ShouldBe("Granit.Invoicing.InvoiceQuery");
        typed.Aggregation.ShouldBe(AggregateFunction.Sum);
        typed.Field.ShouldBe("Total");
        typed.KeyFormats.ShouldBeNull();
    }

    [Fact]
    public void TelemetryDatasource_DefaultsToLastAggregation()
    {
        TelemetryDatasource ds = new("currentDevice", "temperature");

        ds.Aggregation.ShouldBe(TelemetryAggregation.Last);

        string json = JsonSerializer.Serialize<Datasource>(ds);
        json.ShouldContain("\"kind\":\"iot-telemetry\"");
    }

    [Fact]
    public void Factory_Metric_ProducesMetricDatasource()
    {
        MetricDatasource ds = Datasource.Metric("Sample.Metric");

        ds.MetricName.ShouldBe("Sample.Metric");
    }

    [Fact]
    public void Factory_QueryAggregate_AcceptsKeyFormats()
    {
        QueryAggregateDatasource ds = Datasource.QueryAggregate(
            "Sample.Q",
            AggregateFunction.Avg,
            field: "amount",
            keyFormats:
            [
                new DataKeyFormat("paid", Color: "#00aa00", Unit: "€"),
                new DataKeyFormat("unpaid", Color: "#cc0000", Unit: "€"),
            ]);

        ds.KeyFormats.ShouldNotBeNull();
        ds.KeyFormats.Count.ShouldBe(2);
        ds.KeyFormats[0].Color.ShouldBe("#00aa00");
    }

    [Fact]
    public void Factory_Telemetry_PassesEntityAliasAndKey()
    {
        TelemetryDatasource ds = Datasource.Telemetry(
            entityAlias: "currentDevice",
            telemetryKey: "rpm",
            aggregation: TelemetryAggregation.Avg);

        ds.EntityAlias.ShouldBe("currentDevice");
        ds.TelemetryKey.ShouldBe("rpm");
        ds.Aggregation.ShouldBe(TelemetryAggregation.Avg);
    }

    [Fact]
    public void TelemetryAggregation_EnumOrderingIsStable()
    {
        ((int)TelemetryAggregation.Last).ShouldBe(0);
        ((int)TelemetryAggregation.Avg).ShouldBe(1);
        ((int)TelemetryAggregation.Sum).ShouldBe(2);
        ((int)TelemetryAggregation.Min).ShouldBe(3);
        ((int)TelemetryAggregation.Max).ShouldBe(4);
        ((int)TelemetryAggregation.Count).ShouldBe(5);
    }

    [Fact]
    public void Datasource_RoundTripsThroughJson_PreservingKind()
    {
        Datasource[] originals =
        [
            new MetricDatasource("M1"),
            new QueryAggregateDatasource("Q1", AggregateFunction.Count, null),
            new TelemetryDatasource("alias1", "key1"),
        ];

        foreach (Datasource original in originals)
        {
            string json = JsonSerializer.Serialize(original);
            Datasource? decoded = JsonSerializer.Deserialize<Datasource>(json);

            decoded.ShouldNotBeNull();
            decoded.GetType().ShouldBe(original.GetType());
        }
    }

    [Fact]
    public void DataKeyFormat_AllOptionalFieldsDefaultToNull()
    {
        DataKeyFormat fmt = new("series-1");

        fmt.Key.ShouldBe("series-1");
        fmt.LabelLocalizationKey.ShouldBeNull();
        fmt.Color.ShouldBeNull();
        fmt.Unit.ShouldBeNull();
        fmt.Decimals.ShouldBeNull();
    }
}

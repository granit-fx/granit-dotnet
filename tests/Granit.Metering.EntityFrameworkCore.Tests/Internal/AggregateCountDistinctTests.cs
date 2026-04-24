using Granit.Metering.Domain;
using Granit.Metering.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Metering.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Pure-function tests for the CountDistinct aggregation path on
/// <see cref="EfAggregationRunner.Aggregate"/>. The aggregator extracts a JSON
/// property from each event's <c>Metadata</c>, dedupes the values, and returns
/// the distinct count. Events with null/empty/invalid metadata or missing
/// property are excluded entirely.
/// </summary>
public sealed class AggregateCountDistinctTests
{
    private static MeterDefinition NewCountDistinctMeter(string distinctProperty) =>
        MeterDefinition.Create(
            Guid.NewGuid(),
            "Active Users",
            "users",
            AggregationType.CountDistinct,
            description: null,
            productId: null,
            distinctProperty: distinctProperty);

    private static MeterEvent NewEvent(string? metadataJson, decimal quantity = 1m) =>
        MeterEvent.Create(
            Guid.NewGuid(),
            meterDefinitionId: Guid.NewGuid(),
            idempotencyKey: Guid.NewGuid().ToString("N"),
            quantity: quantity,
            timestamp: DateTimeOffset.UtcNow,
            metadata: metadataJson);

    [Fact]
    public void Aggregate_AllDistinct_ShouldReturnEventCount()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":"alice"}"""),
            NewEvent("""{"user_id":"bob"}"""),
            NewEvent("""{"user_id":"carol"}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(3m);
    }

    [Fact]
    public void Aggregate_AllSame_ShouldReturnOne()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = Enumerable.Range(0, 5)
            .Select(_ => NewEvent("""{"user_id":"alice"}"""))
            .ToList();

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(1m);
    }

    [Fact]
    public void Aggregate_MixedDuplicates_ShouldReturnDistinctCount()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":"A"}"""),
            NewEvent("""{"user_id":"A"}"""),
            NewEvent("""{"user_id":"A"}"""),
            NewEvent("""{"user_id":"A"}"""),
            NewEvent("""{"user_id":"A"}"""),
            NewEvent("""{"user_id":"B"}"""),
            NewEvent("""{"user_id":"B"}"""),
            NewEvent("""{"user_id":"B"}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(2m);
    }

    [Fact]
    public void Aggregate_MissingProperty_ShouldExcludeEvent()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":"alice"}"""),
            NewEvent("""{"unrelated":"value"}"""),
            NewEvent("""{"user_id":"bob"}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(2m);
    }

    [Fact]
    public void Aggregate_NullOrEmptyMetadata_ShouldExcludeEvent()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":"alice"}"""),
            NewEvent(metadataJson: null),
            NewEvent(""),
            NewEvent("   "),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(1m);
    }

    [Fact]
    public void Aggregate_InvalidJson_ShouldExcludeEvent()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":"alice"}"""),
            NewEvent("not-json-at-all"),
            NewEvent("{ malformed"),
            NewEvent("""{"user_id":"bob"}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(2m);
    }

    [Fact]
    public void Aggregate_NumericValues_ShouldBeStringified()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":1}"""),
            NewEvent("""{"user_id":"1"}"""), // distinct from numeric 1 — raw text "1" vs JSON string "1"
            NewEvent("""{"user_id":2}"""),
        };

        // JsonValueKind.Number GetRawText() = "1"; String GetString() = "1" — collide ordinally.
        EfAggregationRunner.Aggregate(meter, events).ShouldBe(2m);
    }

    [Fact]
    public void Aggregate_NestedOrArrayValues_ShouldBeExcluded()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"user_id":{"nested":"obj"}}"""),
            NewEvent("""{"user_id":["array"]}"""),
            NewEvent("""{"user_id":"alice"}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(1m);
    }

    [Fact]
    public void Aggregate_BooleanValues_ShouldBeStringified()
    {
        MeterDefinition meter = NewCountDistinctMeter("flag");
        var events = new List<MeterEvent>
        {
            NewEvent("""{"flag":true}"""),
            NewEvent("""{"flag":false}"""),
            NewEvent("""{"flag":true}"""),
        };

        EfAggregationRunner.Aggregate(meter, events).ShouldBe(2m);
    }

    [Fact]
    public void Aggregate_NoEvents_ShouldReturnZero()
    {
        MeterDefinition meter = NewCountDistinctMeter("user_id");

        EfAggregationRunner.Aggregate(meter, []).ShouldBe(0m);
    }
}

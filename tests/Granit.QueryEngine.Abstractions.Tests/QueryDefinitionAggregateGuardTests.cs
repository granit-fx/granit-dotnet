using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryDefinitionAggregateGuardTests
{
    private sealed class Order
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    [Fact]
    public void Numeric_aggregates_are_recorded()
    {
        QueryDefinitionBuilder<Order> builder = new();

        builder
            .Aggregate(o => o.Amount, AggregateFunction.Sum, "totalAmount")
            .Aggregate(o => o.Quantity, AggregateFunction.Avg, "avgQuantity");

        builder.Aggregates.Count.ShouldBe(2);
        builder.Aggregates[0].Alias.ShouldBe("totalAmount");
        builder.Aggregates[0].Function.ShouldBe(AggregateFunction.Sum);
    }

    [Fact]
    public void Count_accepts_a_non_numeric_property()
    {
        QueryDefinitionBuilder<Order> builder = new();

        builder.Aggregate(o => o.Reference, AggregateFunction.Count, "orderCount");

        builder.Aggregates.ShouldHaveSingleItem().Function.ShouldBe(AggregateFunction.Count);
    }

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public void Non_numeric_property_is_rejected_for_numeric_functions(AggregateFunction function)
    {
        QueryDefinitionBuilder<Order> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            builder.Aggregate(o => o.Reference, function, "bad"));

        ex.Message.ShouldContain("not numeric");
    }

    [Fact]
    public void Date_property_is_rejected_for_min_max()
    {
        QueryDefinitionBuilder<Order> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Aggregate(o => o.CreatedAt, AggregateFunction.Max, "latest"));
    }

    [Fact]
    public void Duplicate_alias_is_rejected_case_insensitively()
    {
        QueryDefinitionBuilder<Order> builder = new();
        builder.Aggregate(o => o.Amount, AggregateFunction.Sum, "totalAmount");

        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            builder.Aggregate(o => o.Quantity, AggregateFunction.Sum, "TOTALAMOUNT"));

        ex.Message.ShouldContain("already declared");
    }

    [Fact]
    public void Slot_cap_is_enforced()
    {
        QueryDefinitionBuilder<Order> builder = new();
        for (int i = 0; i < QueryEngineDefaults.MaxAggregates; i++)
        {
            builder.Aggregate(o => o.Amount, AggregateFunction.Sum, $"agg{i}");
        }

        Should.Throw<InvalidOperationException>(() =>
            builder.Aggregate(o => o.Amount, AggregateFunction.Sum, "oneTooMany"));
    }

    [Fact]
    public void Blank_alias_is_rejected()
    {
        QueryDefinitionBuilder<Order> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Aggregate(o => o.Amount, AggregateFunction.Sum, " "));
    }
}

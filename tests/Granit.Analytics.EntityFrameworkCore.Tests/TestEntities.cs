using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.EntityFrameworkCore.Tests;

internal enum OrderStatus { Pending, Paid, Cancelled }

internal sealed class Order
{
    public Guid Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int LineCount { get; init; }
    public long TotalCents { get; init; }
    public double DiscountRatio { get; init; }
    public OrderStatus Status { get; init; }
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.CustomerName).IsRequired();
            e.Property(o => o.Status).HasConversion<string>();
        });
}

internal sealed class OrderQueryDefinition : QueryDefinition<Order>
{
    public override string Name => "Test.Orders";

    protected override void Configure(QueryDefinitionBuilder<Order> builder) =>
        builder
            .Column(o => o.CustomerName, c => c.Label("Customer").Filterable())
            .Column(o => o.Amount, c => c.Label("Amount").Filterable().Sortable())
            .Column(o => o.Status, c => c.Label("Status").Filterable())
            .Column(o => o.LineCount, c => c.Label("Lines").Filterable())
            .DefaultPageSize(50);
}

internal sealed class OrderCountMetricDefinition : MetricDefinition<Order, int>
{
    public override string Name => "Test.OrderCount";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Count;
    public override Expression<Func<Order, int?>>? Selector => null;
}

internal sealed class OrderAmountSumMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.OrderAmountSum";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
    public override string? CurrencyCode => "EUR";
}

internal sealed class OrderAmountAvgMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.OrderAmountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
    public override string? CurrencyCode => "EUR";
}

internal sealed class OrderAmountMinMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.OrderAmountMin";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Min;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountMaxMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.OrderAmountMax";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Max;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderLineCountSumMetricDefinition : MetricDefinition<Order, int>
{
    public override string Name => "Test.OrderLineCountSum";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, int?>>? Selector => o => o.LineCount;
}

internal sealed class OrderTotalCentsSumMetricDefinition : MetricDefinition<Order, long>
{
    public override string Name => "Test.OrderTotalCentsSum";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, long?>>? Selector => o => o.TotalCents;
}

internal sealed class OrderDiscountAvgMetricDefinition : MetricDefinition<Order, double>
{
    public override string Name => "Test.OrderDiscountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Percentage;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, double?>>? Selector => o => o.DiscountRatio;
}

internal sealed class OrderDiscountSumMetricDefinition : MetricDefinition<Order, double>
{
    public override string Name => "Test.OrderDiscountSum";
    public override MetricValueKind ValueKind => MetricValueKind.Number;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, double?>>? Selector => o => o.DiscountRatio;
}

internal sealed class OrderLineCountAvgMetricDefinition : MetricDefinition<Order, int>
{
    public override string Name => "Test.OrderLineCountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, int?>>? Selector => o => o.LineCount;
}

internal sealed class OrderTotalCentsAvgMetricDefinition : MetricDefinition<Order, long>
{
    public override string Name => "Test.OrderTotalCentsAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, long?>>? Selector => o => o.TotalCents;
}

internal sealed class OrderLineCountMinMetricDefinition : MetricDefinition<Order, int>
{
    public override string Name => "Test.OrderLineCountMin";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Min;
    public override Expression<Func<Order, int?>>? Selector => o => o.LineCount;
}

internal sealed class OrderTotalCentsMaxMetricDefinition : MetricDefinition<Order, long>
{
    public override string Name => "Test.OrderTotalCentsMax";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Max;
    public override Expression<Func<Order, long?>>? Selector => o => o.TotalCents;
}

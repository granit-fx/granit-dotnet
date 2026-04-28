using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

internal sealed class Order
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public int LineCount { get; init; }
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Order>(e => e.HasKey(o => o.Id));
}

internal sealed class OrderQueryDefinition : QueryDefinition<Order>
{
    public override string Name => "Test.Orders";

    protected override void Configure(QueryDefinitionBuilder<Order> builder) =>
        builder
            .Column(o => o.Amount, c => c.Filterable())
            .Column(o => o.LineCount, c => c.Filterable())
            .DefaultPageSize(50);
}

internal sealed class OrderAmountSumMetric : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountSum";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountAvgMetric : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountMinMetric : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountMin";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Min;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountMaxMetric : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountMax";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Max;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderCountMetric : MetricDefinition<Order, int>
{
    public override string Name => "Test.Count";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Count;
    public override Expression<Func<Order, int?>>? Selector => null;
}

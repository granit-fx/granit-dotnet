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

internal sealed class Customer
{
    public Guid Id { get; init; }
    public string Country { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e => e.HasKey(o => o.Id));
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Country).HasMaxLength(64).IsRequired();
            e.Property(c => c.Status).HasMaxLength(64).IsRequired();
        });
    }
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

internal sealed class CustomerQueryDefinition : QueryDefinition<Customer>
{
    public override string Name => "Test.Customers";

    protected override void Configure(QueryDefinitionBuilder<Customer> builder) =>
        builder
            .Column(c => c.Country, col => col.Filterable())
            .Column(c => c.Status, col => col.Filterable())
            .Column(c => c.Revenue, col => col.Filterable())
            .DefaultPageSize(50);
}

internal sealed class CustomerSource(TestDbContext db) : IQueryableSource<Customer>
{
    private readonly TestDbContext _db = db;
    public IQueryable<Customer> GetQueryable() => _db.Customers.AsNoTracking();
}

internal sealed class OrderAmountSumMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountSum";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountAvgMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountMinMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountMin";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Min;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderAmountMaxMetricDefinition : MetricDefinition<Order, decimal>
{
    public override string Name => "Test.AmountMax";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Max;
    public override Expression<Func<Order, decimal?>>? Selector => o => o.Amount;
}

internal sealed class OrderCountMetricDefinition : MetricDefinition<Order, int>
{
    public override string Name => "Test.Count";
    public override MetricValueKind ValueKind => MetricValueKind.Count;
    public override AggregateFunction Aggregation => AggregateFunction.Count;
    public override Expression<Func<Order, int?>>? Selector => null;
}

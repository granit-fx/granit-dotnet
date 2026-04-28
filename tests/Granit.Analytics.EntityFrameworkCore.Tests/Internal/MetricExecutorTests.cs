using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Internal;

public sealed class MetricExecutorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IQueryEngine<Order> _engine = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        _db.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), CustomerName = "Alice", Amount = 120.50m, LineCount = 3, TotalCents = 12050, DiscountRatio = 0.10, Status = OrderStatus.Paid },
            new Order { Id = Guid.NewGuid(), CustomerName = "Bob", Amount = 80.00m, LineCount = 1, TotalCents = 8000, DiscountRatio = 0.00, Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), CustomerName = "Charlie", Amount = 250.75m, LineCount = 5, TotalCents = 25075, DiscountRatio = 0.20, Status = OrderStatus.Paid },
            new Order { Id = Guid.NewGuid(), CustomerName = "Diane", Amount = 50.00m, LineCount = 2, TotalCents = 5000, DiscountRatio = 0.05, Status = OrderStatus.Cancelled });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        (_provider, _engine) = TestEngineFactory.Build<Order, OrderQueryDefinition>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        await _provider.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Count_NoFilter_ReturnsAllRows()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderCountMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(4);
    }

    [Fact]
    public async Task Count_WithFilter_AppliesFilter()
    {
        MetricExecutor<Order, int> executor = new(_engine);
        QueryRequest request = new() { Filter = new Dictionary<string, string> { ["Status.eq"] = "Paid" } };

        int? result = await executor.ExecuteAsync(
            new OrderCountMetricDefinition(), _db.Orders, request, TestContext.Current.CancellationToken);

        result.ShouldBe(2);
    }

    [Fact]
    public async Task Sum_Decimal_ReturnsExactSum()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(120.50m + 80.00m + 250.75m + 50.00m);
    }

    [Fact]
    public async Task Sum_Int_ReturnsExactSum()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderLineCountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(3 + 1 + 5 + 2);
    }

    [Fact]
    public async Task Sum_Long_ReturnsExactSum()
    {
        MetricExecutor<Order, long> executor = new(_engine);

        long? result = await executor.ExecuteAsync(
            new OrderTotalCentsSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(12050L + 8000L + 25075L + 5000L);
    }

    [Fact]
    public async Task Avg_Decimal_ReturnsAverage()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        decimal expected = (120.50m + 80.00m + 250.75m + 50.00m) / 4m;
        result!.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task Avg_Double_ReturnsAverage()
    {
        MetricExecutor<Order, double> executor = new(_engine);

        double? result = await executor.ExecuteAsync(
            new OrderDiscountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        double expected = (0.10 + 0.00 + 0.20 + 0.05) / 4.0;
        result!.Value.ShouldBe(expected, 1e-10);
    }

    [Fact]
    public async Task Min_Decimal_ReturnsLowest()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountMinMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(50.00m);
    }

    [Fact]
    public async Task Max_Decimal_ReturnsHighest()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountMaxMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(250.75m);
    }

    [Fact]
    public async Task ExecuteAsync_NullMetric_Throws()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await executor.ExecuteAsync(null!, _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SumWithoutSelector_Throws()
    {
        MetricExecutor<Order, int> executor = new(_engine);
        BrokenSumWithoutSelector metric = new();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await executor.ExecuteAsync(metric, _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken));
    }

    private sealed class BrokenSumWithoutSelector : Granit.Analytics.Metrics.MetricDefinition<Order, int>
    {
        public override string Name => "Test.BrokenSum";
        public override Granit.Analytics.Metrics.MetricValueKind ValueKind => Granit.Analytics.Metrics.MetricValueKind.Number;
        public override Granit.QueryEngine.Filtering.AggregateFunction Aggregation => Granit.QueryEngine.Filtering.AggregateFunction.Sum;
        public override System.Linq.Expressions.Expression<Func<Order, int?>>? Selector => null;
    }
}

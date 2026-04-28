using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Empty;

/// <summary>
/// Locks the empty-set semantics promised by <c>MetricDefinition</c>:
/// <list type="bullet">
///   <item><c>Count</c> and <c>Sum</c> over an empty set return <c>0</c> (mathematically defined).</item>
///   <item><c>Avg</c>, <c>Min</c>, <c>Max</c> over an empty set return <c>null</c> ("no data" — never zero, never an exception).</item>
/// </list>
/// Without this contract, a tenant's onboarding day — the very first time a user looks at a
/// dashboard with no data yet — would either show "Average invoice amount: 0 €" (misleading) or
/// crash with <see cref="InvalidOperationException"/>. Story #1374 of EPIC #1366.
/// </summary>
public sealed class EmptySetSemanticsTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IQueryEngine<Order> _engine = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Empty database — no inserts. Every query returns 0 rows.
        (_provider, _engine) = TestEngineFactory.Build<Order, OrderQueryDefinition>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        await _provider.DisposeAsync();
    }

    // ── Count: empty → 0 (per TValue) ────────────────────────────────────────

    [Fact]
    public async Task Count_Empty_Int_ReturnsZero()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderCountMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(0);
    }

    // ── Sum: empty → 0 across all numeric TValue ─────────────────────────────

    [Fact]
    public async Task Sum_Empty_Decimal_ReturnsZero()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(0m);
    }

    [Fact]
    public async Task Sum_Empty_Int_ReturnsZero()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderLineCountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(0);
    }

    [Fact]
    public async Task Sum_Empty_Long_ReturnsZero()
    {
        MetricExecutor<Order, long> executor = new(_engine);

        long? result = await executor.ExecuteAsync(
            new OrderTotalCentsSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(0L);
    }

    [Fact]
    public async Task Sum_Empty_Double_ReturnsZero()
    {
        MetricExecutor<Order, double> executor = new(_engine);

        double? result = await executor.ExecuteAsync(
            new OrderDiscountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(0.0);
    }

    // ── Avg: empty → null across all numeric TValue ──────────────────────────

    [Fact]
    public async Task Avg_Empty_Decimal_ReturnsNull_NotZero()
    {
        // Critical regression test — the EF Core default for Avg over empty is to throw
        // InvalidOperationException. The Selector being typed nullable plus this assertion
        // are what guarantees the contract holds.
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Avg_Empty_Int_ReturnsNull()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderLineCountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Avg_Empty_Long_ReturnsNull()
    {
        MetricExecutor<Order, long> executor = new(_engine);

        long? result = await executor.ExecuteAsync(
            new OrderTotalCentsAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Avg_Empty_Double_ReturnsNull()
    {
        MetricExecutor<Order, double> executor = new(_engine);

        double? result = await executor.ExecuteAsync(
            new OrderDiscountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Min: empty → null ────────────────────────────────────────────────────

    [Fact]
    public async Task Min_Empty_Decimal_ReturnsNull()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountMinMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Min_Empty_Int_ReturnsNull()
    {
        MetricExecutor<Order, int> executor = new(_engine);

        int? result = await executor.ExecuteAsync(
            new OrderLineCountMinMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Max: empty → null ────────────────────────────────────────────────────

    [Fact]
    public async Task Max_Empty_Decimal_ReturnsNull()
    {
        MetricExecutor<Order, decimal> executor = new(_engine);

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountMaxMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Max_Empty_Long_ReturnsNull()
    {
        MetricExecutor<Order, long> executor = new(_engine);

        long? result = await executor.ExecuteAsync(
            new OrderTotalCentsMaxMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Single-row sanity (the matrix's "1" cell) ────────────────────────────

    [Fact]
    public async Task SingleRow_Avg_Decimal_EqualsThatRow()
    {
        _db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = "Alice",
            Amount = 42.50m,
            LineCount = 1,
            TotalCents = 4250,
            DiscountRatio = 0.05,
            Status = OrderStatus.Paid,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<Order, decimal> executor = new(_engine);
        decimal? result = await executor.ExecuteAsync(
            new OrderAmountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(42.50m);
    }

    [Fact]
    public async Task SingleRow_Min_EqualsMax_EqualsThatRow()
    {
        _db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = "Bob",
            Amount = 99.99m,
            LineCount = 7,
            TotalCents = 9999,
            DiscountRatio = 0.10,
            Status = OrderStatus.Pending,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<Order, decimal> minExecutor = new(_engine);
        MetricExecutor<Order, decimal> maxExecutor = new(_engine);
        decimal? min = await minExecutor.ExecuteAsync(
            new OrderAmountMinMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);
        decimal? max = await maxExecutor.ExecuteAsync(
            new OrderAmountMaxMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        min.ShouldBe(99.99m);
        max.ShouldBe(99.99m);
    }

    // ── Filter that excludes everything → empty-set semantics still apply ───

    [Fact]
    public async Task FilterEliminatesAllRows_Sum_ReturnsZero()
    {
        // Insert rows but apply a filter that excludes them all — should behave like a true empty set.
        _db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = "Carol",
            Amount = 100m,
            LineCount = 1,
            TotalCents = 10000,
            DiscountRatio = 0.0,
            Status = OrderStatus.Paid,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<Order, decimal> executor = new(_engine);
        QueryRequest request = new() { Filter = new Dictionary<string, string> { ["Status.eq"] = "Cancelled" } };

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountSumMetricDefinition(), _db.Orders, request, TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task FilterEliminatesAllRows_Avg_ReturnsNull()
    {
        _db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = "Carol",
            Amount = 100m,
            LineCount = 1,
            TotalCents = 10000,
            DiscountRatio = 0.0,
            Status = OrderStatus.Paid,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<Order, decimal> executor = new(_engine);
        QueryRequest request = new() { Filter = new Dictionary<string, string> { ["Status.eq"] = "Cancelled" } };

        decimal? result = await executor.ExecuteAsync(
            new OrderAmountAvgMetricDefinition(), _db.Orders, request, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}

using Granit.QueryEngine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the contract added to <see cref="IQueryEngine{TEntity}"/> by story #1376
/// (rolled in from spike #1372). The MetricExecutor relies on this method to apply the
/// same filter pipeline as grids, with no projection / sort / pagination.
/// </summary>
public sealed class BuildFilteredQueryTests : IAsyncLifetime
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
            new Order { Id = Guid.NewGuid(), CustomerName = "Alice", Amount = 100m, LineCount = 1, TotalCents = 10000, DiscountRatio = 0.0, Status = OrderStatus.Paid },
            new Order { Id = Guid.NewGuid(), CustomerName = "Bob", Amount = 200m, LineCount = 1, TotalCents = 20000, DiscountRatio = 0.0, Status = OrderStatus.Paid },
            new Order { Id = Guid.NewGuid(), CustomerName = "Eve", Amount = 300m, LineCount = 1, TotalCents = 30000, DiscountRatio = 0.0, Status = OrderStatus.Cancelled });

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
    public async Task NoFilter_ReturnsAllRows()
    {
        IQueryable<Order> filtered = _engine.BuildFilteredQuery(_db.Orders, new QueryRequest());

        int count = await filtered.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(3);
    }

    [Fact]
    public async Task WithFilter_AppliesPredicate()
    {
        QueryRequest request = new() { Filter = new Dictionary<string, string> { ["Status.eq"] = "Paid" } };

        IQueryable<Order> filtered = _engine.BuildFilteredQuery(_db.Orders, request);

        int count = await filtered.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsUnsortedUnpaginated_ReadyForAggregation()
    {
        IQueryable<Order> filtered = _engine.BuildFilteredQuery(_db.Orders, new QueryRequest());

        // The whole point: aggregations against the filtered queryable produce a single SQL
        // statement, no extra rows materialized. We test the contract by chaining a Sum.
        decimal total = await filtered.SumAsync(o => o.Amount, TestContext.Current.CancellationToken);

        total.ShouldBe(100m + 200m + 300m);
    }

    [Fact]
    public void NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _engine.BuildFilteredQuery(null!, new QueryRequest()));
    }

    [Fact]
    public void NullRequest_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _engine.BuildFilteredQuery(_db.Orders, null!));
    }
}

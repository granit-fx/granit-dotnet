using Granit.Analytics.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Asserts that empty-set semantics promised by the analytics executor on Sqlite (the
/// unit-test provider) hold byte-for-byte against the PostgreSQL engine. Story #1374.
/// </summary>
public sealed class EmptySetSemanticsPostgresParityTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IMetricExecutor<Order, decimal> _decimalExecutor = null!;
    private IMetricExecutor<Order, int> _intExecutor = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        _db = new TestDbContext(options);
        // EnsureCreated is idempotent — the Testcontainers PostgreSQL container is fresh
        // per test class, so the schema does not yet exist on first call.
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Drop any leftover rows in case xUnit re-uses the same fixture across tests.
        await _db.Orders.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // No inserts — the table is empty, every aggregate hits the empty-set path.
        (_provider, IQueryEngine<Order> engine) = TestEngineFactory.Build<Order, OrderQueryDefinition>();

        // Construct the typed executors directly via DI on top of the resolved IQueryEngine.
        ServiceCollection executorServices = new();
        executorServices.AddSingleton(engine);
        executorServices.AddScoped(typeof(IMetricExecutor<,>),
            typeof(GranitAnalyticsEntityFrameworkCoreModule).Assembly
                .GetType("Granit.Analytics.EntityFrameworkCore.Internal.MetricExecutor`2", throwOnError: true)!);

        ServiceProvider executorProvider = executorServices.BuildServiceProvider();
        _decimalExecutor = executorProvider.GetRequiredService<IMetricExecutor<Order, decimal>>();
        _intExecutor = executorProvider.GetRequiredService<IMetricExecutor<Order, int>>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Count_Empty_Postgres_ReturnsZero()
    {
        int? result = await _intExecutor.ExecuteAsync(
            new OrderCountMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task Sum_Empty_Postgres_ReturnsZero()
    {
        decimal? result = await _decimalExecutor.ExecuteAsync(
            new OrderAmountSumMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task Avg_Empty_Postgres_ReturnsNull()
    {
        // PostgreSQL: AVG over empty returns NULL. EF Core surfaces it as null because the
        // selector is typed nullable. This assertion is the load-bearing parity check.
        decimal? result = await _decimalExecutor.ExecuteAsync(
            new OrderAmountAvgMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Min_Empty_Postgres_ReturnsNull()
    {
        decimal? result = await _decimalExecutor.ExecuteAsync(
            new OrderAmountMinMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Max_Empty_Postgres_ReturnsNull()
    {
        decimal? result = await _decimalExecutor.ExecuteAsync(
            new OrderAmountMaxMetricDefinition(), _db.Orders, new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}

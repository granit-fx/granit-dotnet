using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Filtering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Pins the joined-metric dispatch path of <see cref="MetricExecutor{TEntity, TValue}"/>:
/// a <see cref="JoinedMetricDefinition{TEntity, TJoined, TValue}"/> projects across
/// (<c>TEntity</c> ⋈ <c>TJoined</c>) before aggregating, with the joined source
/// resolved from DI at request time. Same empty-set contract as the single-table
/// path (story A3 #1374): Sum-of-empty = 0; Avg-of-empty = null.
/// </summary>
public sealed class JoinedMetricExecutorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private JoinedTestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IQueryEngine<JoinedOrder> _engine = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        DbContextOptions<JoinedTestDbContext> options = new DbContextOptionsBuilder<JoinedTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new JoinedTestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        var eurId = Guid.NewGuid();
        var usdId = Guid.NewGuid();

        _db.Currencies.AddRange(
            new Currency { Id = eurId, Code = "EUR", ConversionToHomeCurrency = 1.0m },
            new Currency { Id = usdId, Code = "USD", ConversionToHomeCurrency = 0.9m });

        _db.JoinedOrders.AddRange(
            new JoinedOrder { Id = Guid.NewGuid(), Amount = 100m, CurrencyId = eurId },          // → 100 EUR-equiv
            new JoinedOrder { Id = Guid.NewGuid(), Amount = 100m, CurrencyId = usdId },          // → 90 EUR-equiv
            new JoinedOrder { Id = Guid.NewGuid(), Amount = 200m, CurrencyId = usdId });         // → 180 EUR-equiv

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Build IQueryEngine<JoinedOrder> + IQueryableSource<Currency> in a single
        // DI provider so the executor can resolve both at request time. Mirrors
        // TestEngineFactory's setup with the joined source layered in.
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOptions<Granit.QueryEngine.Options.QueryEngineOptions>();
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Granit.QueryEngine.Options.QueryEngineOptions>>().Value);
        services.AddGranitQueryEngine();
        services.AddScoped(typeof(IQueryEngine<>),
            typeof(Granit.QueryEngine.EntityFrameworkCore.GranitQueryEngineEntityFrameworkCoreModule)
                .Assembly
                .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!);
        services.AddQueryDefinition<JoinedOrder, JoinedOrderQueryDefinition>();
        services.AddSingleton<IQueryableSource<Currency>>(_ => new TestCurrencyQueryableSource(_db));

        _provider = services.BuildServiceProvider();
        _engine = _provider.GetRequiredService<IQueryEngine<JoinedOrder>>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        await _provider.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task JoinedSum_ReturnsWeightedTotalAcrossJoin()
    {
        MetricExecutor<JoinedOrder, decimal> executor = new(_engine, _provider);

        decimal? result = await executor.ExecuteAsync(
            new ConvertedAmountTotalMetricDefinition(),
            _db.JoinedOrders,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        // 100*1.0 + 100*0.9 + 200*0.9 = 100 + 90 + 180 = 370 EUR-equiv.
        result.ShouldBe(370m);
    }

    [Fact]
    public async Task JoinedSum_EmptyBaseSet_ReturnsZero()
    {
        // Drop all orders, leave currencies in place. The metric's projection becomes
        // empty (no left rows to join from), so Sum-of-empty = 0 per contract A3 #1374.
        _db.JoinedOrders.RemoveRange(_db.JoinedOrders);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<JoinedOrder, decimal> executor = new(_engine, _provider);

        decimal? result = await executor.ExecuteAsync(
            new ConvertedAmountTotalMetricDefinition(),
            _db.JoinedOrders,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task JoinedAvg_EmptyBaseSet_ReturnsNull()
    {
        _db.JoinedOrders.RemoveRange(_db.JoinedOrders);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MetricExecutor<JoinedOrder, decimal> executor = new(_engine, _provider);

        decimal? result = await executor.ExecuteAsync(
            new ConvertedAmountAvgMetricDefinition(),
            _db.JoinedOrders,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        // Avg over empty set → null. Same contract as the single-table path.
        result.ShouldBeNull();
    }

    [Fact]
    public async Task JoinedMetric_WithoutServiceProvider_Throws()
    {
        // No IServiceProvider supplied → executor cannot resolve the joined source.
        // Should throw a clear InvalidOperationException at the dispatch point,
        // not crash deep in EF Core.
        MetricExecutor<JoinedOrder, decimal> executor = new(_engine);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                new ConvertedAmountTotalMetricDefinition(),
                _db.JoinedOrders,
                new QueryRequest(),
                TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("IServiceProvider");
    }
}

internal sealed class JoinedOrder
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public Guid CurrencyId { get; init; }
}

internal sealed class Currency
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public decimal ConversionToHomeCurrency { get; init; }
}

internal sealed class JoinedTestDbContext(DbContextOptions<JoinedTestDbContext> options) : DbContext(options)
{
    public DbSet<JoinedOrder> JoinedOrders => Set<JoinedOrder>();
    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JoinedOrder>(e => e.HasKey(o => o.Id));
        modelBuilder.Entity<Currency>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Code).IsRequired();
        });
    }
}

internal sealed class JoinedOrderQueryDefinition : QueryDefinition<JoinedOrder>
{
    public override string Name => "Test.JoinedOrders";

    protected override void Configure(QueryDefinitionBuilder<JoinedOrder> builder) =>
        builder
            .Column(o => o.Amount, c => c.Label("Amount").Filterable().Sortable())
            .DefaultPageSize(50);
}

internal sealed class TestCurrencyQueryableSource(JoinedTestDbContext db) : IQueryableSource<Currency>
{
    public IQueryable<Currency> GetQueryable() => db.Currencies.AsNoTracking();
}

internal sealed class ConvertedAmountTotalMetricDefinition
    : JoinedMetricDefinition<JoinedOrder, Currency, decimal>
{
    public override string Name => "Test.ConvertedAmountTotal";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    public override IQueryable<decimal?> Project(
        IQueryable<JoinedOrder> filteredSource,
        IQueryable<Currency> joinedSource) =>
            from o in filteredSource
            join c in joinedSource on o.CurrencyId equals c.Id
            select (decimal?)(o.Amount * c.ConversionToHomeCurrency);
}

internal sealed class ConvertedAmountAvgMetricDefinition
    : JoinedMetricDefinition<JoinedOrder, Currency, decimal>
{
    public override string Name => "Test.ConvertedAmountAvg";
    public override MetricValueKind ValueKind => MetricValueKind.Currency;
    public override AggregateFunction Aggregation => AggregateFunction.Avg;

    public override IQueryable<decimal?> Project(
        IQueryable<JoinedOrder> filteredSource,
        IQueryable<Currency> joinedSource) =>
            from o in filteredSource
            join c in joinedSource on o.CurrencyId equals c.Id
            select (decimal?)(o.Amount * c.ConversionToHomeCurrency);
}

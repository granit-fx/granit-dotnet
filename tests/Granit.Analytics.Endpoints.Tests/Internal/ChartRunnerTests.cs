using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed integration tests for <see cref="ChartRunner{TEntity}"/>
/// — exercises the SQL-level <c>GroupBy(...).Select(...)</c> expression
/// tree built by <see cref="GroupAggregateExecutor"/> for Sum/Avg/Min/Max,
/// plus the <see cref="IQueryEngine{TEntity}.ExecuteGroupedAsync"/>
/// delegation for Count.
/// </summary>
public sealed class ChartRunnerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IQueryEngine<TestItem> _engine = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        (_provider, _engine) = TestEngineFactory.Build<TestItem, TestQueryDefinition>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Count_GroupsRowsViaExecuteGroupedAsync()
    {
        SeedFive();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Count,
            field: null,
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(3m);
        byLabel["Paid"].ShouldBe(2m);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverDecimalField_AggregatesPerGroupViaSql()
    {
        // Five rows: three Open (10, 20, 30 = 60), two Paid (50, 100 = 150).
        SeedFive();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: "Amount",
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(60m);
        byLabel["Paid"].ShouldBe(150m);
    }

    [Fact]
    public async Task ExecuteAsync_Avg_OverDecimalField_ComputesMeanPerGroup()
    {
        SeedFive();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Avg,
            field: "Amount",
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(20m);  // (10 + 20 + 30) / 3
        byLabel["Paid"].ShouldBe(75m);  // (50 + 100) / 2
    }

    [Theory]
    [InlineData(AggregateFunction.Min, 10, 50)]
    [InlineData(AggregateFunction.Max, 30, 100)]
    public async Task ExecuteAsync_MinMax_OverDecimalField_ReturnsExtremumPerGroup(
        AggregateFunction aggregation, int openExpected, int paidExpected)
    {
        SeedFive();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: aggregation,
            field: "Amount",
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(openExpected);
        byLabel["Paid"].ShouldBe(paidExpected);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_AllRowsHaveNullValue_ReturnsZero_NotNull()
    {
        // All rows in the "Open" group have Bonus=null. Sum-of-empty = 0 in SQL
        // (SUM(NULL) is NULL there, but the runner coalesces to 0 to match the
        // metric path's locked semantics #1374).
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 0m, Bonus = null },
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 0m, Bonus = null });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: "Bonus",
            TestContext.Current.CancellationToken);

        result.Buckets.Single().Value.ShouldBe(0m);
    }

    [Theory]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_AvgMinMax_AllNullsInGroup_ReturnsNull(AggregateFunction aggregation)
    {
        // Avg/Min/Max over an all-null column surface as null on the bucket
        // value — locked semantics shared with MetricExecutor #1374.
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 0m, Bonus = null },
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 0m, Bonus = null });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: aggregation,
            field: "Bonus",
            TestContext.Current.CancellationToken);

        result.Buckets.Single().Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("Quantity", typeof(int), 6)]    // 1 + 2 + 3 (Open group) by SeedFive
    [InlineData("BigQuantity", typeof(long), 6)]
    [InlineData("Amount", typeof(decimal), 60)]
    [InlineData("Score", typeof(double), 6)]
    public async Task ExecuteAsync_Sum_AcceptsAllSupportedPrimitiveTypes(string field, Type _, int expectedOpenSum)
    {
        SeedFive();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: field,
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(expectedOpenSum);
    }

    [Fact]
    public async Task ExecuteAsync_NullGroupKey_SurfacesAsNullSentinelLabel()
    {
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Status = null, Amount = 10m },
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 20m });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: "Amount",
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel.ShouldContainKey("(null)");
        byLabel["(null)"].ShouldBe(10m);
        byLabel["Open"].ShouldBe(20m);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownGroupByField_Throws()
    {
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                groupBy: "NotAField",
                aggregation: AggregateFunction.Sum,
                field: "Amount",
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAField");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAggregateField_Throws()
    {
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                groupBy: "Status",
                aggregation: AggregateFunction.Sum,
                field: "NotAColumn",
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedFieldType_Throws()
    {
        // Aggregating Sum over a string column makes no sense — surface a
        // clear NotSupportedException so the dashboard renderer's per-widget
        // isolation surfaces it as Error (config bug, not data bug).
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                groupBy: "Amount",
                aggregation: AggregateFunction.Sum,
                field: "Status",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_NonCountAggregation_WithoutField_Throws()
    {
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                groupBy: "Status",
                aggregation: AggregateFunction.Sum,
                field: null,
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_EmptyGroupBy_Throws(string groupBy)
    {
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                groupBy: groupBy,
                aggregation: AggregateFunction.Count,
                field: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Name_IsSetFromConstructor()
    {
        ChartRunner<TestItem> runner = new("Granit.Test.Items", new TestItemSource(_db), _engine);
        runner.Name.ShouldBe("Granit.Test.Items");
    }

    private void SeedFive()
    {
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 10m, Quantity = 1, BigQuantity = 1L, Score = 1.0d, Bonus = 5m },
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 20m, Quantity = 2, BigQuantity = 2L, Score = 2.0d, Bonus = 10m },
            new TestItem { Id = Guid.NewGuid(), Status = "Open", Amount = 30m, Quantity = 3, BigQuantity = 3L, Score = 3.0d, Bonus = 15m },
            new TestItem { Id = Guid.NewGuid(), Status = "Paid", Amount = 50m, Quantity = 5, BigQuantity = 5L, Score = 5.0d, Bonus = 20m },
            new TestItem { Id = Guid.NewGuid(), Status = "Paid", Amount = 100m, Quantity = 10, BigQuantity = 10L, Score = 10.0d, Bonus = 30m });
    }

    public sealed class TestItem
    {
        public Guid Id { get; set; }
        public string? Status { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public long BigQuantity { get; set; }
        public double Score { get; set; }
        public decimal? Bonus { get; set; }
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestItem> Items => Set<TestItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestItem>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Status);
                b.Property(x => x.Amount);
                b.Property(x => x.Quantity);
                b.Property(x => x.BigQuantity);
                b.Property(x => x.Score);
                b.Property(x => x.Bonus);
            });
        }
    }

    public sealed class TestItemSource(TestDbContext db) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => db.Items.AsQueryable();
    }

    public sealed class TestQueryDefinition : QueryDefinition<TestItem>
    {
        public override string Name => "Test.Items";

        protected override void Configure(QueryDefinitionBuilder<TestItem> builder)
        {
            builder
                .Column(x => x.Status, c => c.Label("Status"))
                .Column(x => x.Amount, c => c.Label("Amount"))
                .AllowGroupBy(x => x.Status);
        }
    }

    /// <summary>
    /// Builds an <see cref="IQueryEngine{TEntity}"/> via DI without taking a
    /// runtime dependency on the internal <c>QueryEngine&lt;T&gt;</c>
    /// implementation. Mirrors the pattern in
    /// <c>Granit.Analytics.EntityFrameworkCore.Tests.TestEngineFactory</c>.
    /// </summary>
    private static class TestEngineFactory
    {
        public static (ServiceProvider Provider, IQueryEngine<TEntity> Engine) Build<TEntity, TDefinition>()
            where TEntity : class
            where TDefinition : QueryDefinition<TEntity>, new()
        {
            ServiceCollection services = new();
            services.AddMetrics();
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddOptions<QueryEngineOptions>();
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<QueryEngineOptions>>().Value);
            services.AddGranitQueryEngine();
            services.AddScoped(typeof(IQueryEngine<>), GetQueryEngineImplementationType());
            services.AddQueryDefinition<TEntity, TDefinition>();

            ServiceProvider provider = services.BuildServiceProvider();
            IQueryEngine<TEntity> engine = provider.GetRequiredService<IQueryEngine<TEntity>>();
            return (provider, engine);
        }

        private static Type GetQueryEngineImplementationType() =>
            typeof(QueryEngine.EntityFrameworkCore.GranitQueryEngineEntityFrameworkCoreModule)
                .Assembly
                .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!;
    }
}

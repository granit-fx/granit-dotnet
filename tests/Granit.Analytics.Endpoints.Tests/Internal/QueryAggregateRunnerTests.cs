using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed integration tests for <see cref="QueryAggregateRunner{TEntity}"/>.
/// Pin the empty-set semantics shared with the metric path (Count/Sum → 0,
/// Avg/Min/Max → null), the Sum/Avg/Min/Max field-selector reflection, and
/// the misconfiguration path (unknown field, unsupported field type).
/// </summary>
public sealed class QueryAggregateRunnerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Count_OverPopulatedTable_ReturnsRowCount()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, TestContext.Current.CancellationToken);

        result.ShouldBe(3m);
    }

    [Fact]
    public async Task ExecuteAsync_Count_OverEmptyTable_ReturnsZero_NotNull()
    {
        // Locked semantics shared with MetricExecutor (story #1374): Count over
        // an empty set is always 0, never null.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverDecimalField_ReturnsTotal()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBe(60m); // 10 + 20 + 30
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverEmptySet_ReturnsZero_NotNull()
    {
        // Sum-of-empty is 0 (mathematical identity). Never null.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_Avg_OverDecimalField_ReturnsMean()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Avg, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBe(20m); // (10 + 20 + 30) / 3
    }

    [Fact]
    public async Task ExecuteAsync_Avg_OverEmptySet_ReturnsNull()
    {
        // Avg-of-empty is undefined (division by zero). Never zero.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Avg, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(AggregateFunction.Min, 10)]
    [InlineData(AggregateFunction.Max, 30)]
    public async Task ExecuteAsync_MinMax_OverDecimalField_ReturnsExtremum(AggregateFunction aggregation, int expected)
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            aggregation, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_MinMax_OverEmptySet_ReturnsNull(AggregateFunction aggregation)
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            aggregation, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverIntField_RoundsTripsViaInt()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Quantity", TestContext.Current.CancellationToken);

        result.ShouldBe(6m); // 1 + 2 + 3
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverDoubleField_RoundsTripsViaDouble()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Score", TestContext.Current.CancellationToken);

        result.ShouldBe(6m); // 1.0 + 2.0 + 3.0
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverNullableDecimalField_TreatsAllNullAsEmptySet()
    {
        // EF Core's SumAsync on a nullable column with all rows NULL returns
        // null — the runner must coalesce to 0, matching Sum-of-empty semantics.
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Amount = 0m, Quantity = 0, Score = 0d, Bonus = null },
            new TestItem { Id = Guid.NewGuid(), Amount = 0m, Quantity = 0, Score = 0d, Bonus = null });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Bonus", TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownField_Throws()
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: "NotAColumn", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
        ex.Message.ShouldContain(nameof(TestItem));
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedFieldType_Throws()
    {
        // Aggregating Sum over a string column makes no sense — surface a clear
        // error so the dashboard renderer's per-widget isolation surfaces it
        // as Error (config bug, not data bug).
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: "Label", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_NonCountAggregation_WithoutField_Throws()
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_FieldName_IsCaseInsensitive()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "amount", TestContext.Current.CancellationToken);

        result.ShouldBe(60m);
    }

    private void SeedThree()
    {
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Amount = 10m, Quantity = 1, Score = 1.0d, Label = "a", Bonus = 5m },
            new TestItem { Id = Guid.NewGuid(), Amount = 20m, Quantity = 2, Score = 2.0d, Label = "b", Bonus = 10m },
            new TestItem { Id = Guid.NewGuid(), Amount = 30m, Quantity = 3, Score = 3.0d, Label = "c", Bonus = 15m });
    }

    private sealed class TestItem
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public double Score { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal? Bonus { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestItem> Items => Set<TestItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestItem>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Amount);
                b.Property(x => x.Quantity);
                b.Property(x => x.Score);
                b.Property(x => x.Label);
                b.Property(x => x.Bonus);
            });
        }
    }

    private sealed class TestItemSource(TestDbContext db) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => db.Items.AsQueryable();
    }
}

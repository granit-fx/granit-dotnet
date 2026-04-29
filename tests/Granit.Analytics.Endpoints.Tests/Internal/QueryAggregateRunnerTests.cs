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
/// Pin the empty-set semantics shared with the metric path (Count over zero
/// rows = 0, never null) and the Count-only contract for B3-2bis.
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
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), IsOpen = true },
            new TestItem { Id = Guid.NewGuid(), IsOpen = true },
            new TestItem { Id = Guid.NewGuid(), IsOpen = false });
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
        // an empty set is always 0, never null. KPI tiles render "0" with the
        // Count value-kind label, never the no-data placeholder.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_NonCountAggregations_ReturnNull_UntilFollowUpSliceLands(AggregateFunction aggregation)
    {
        // B3-2bis ships Count only. Sum / Avg / Min / Max return null so the
        // caller maps them onto Widget:Unavailable.QueryAggregateOperationNotImplemented.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db));

        decimal? result = await runner.ExecuteAsync(
            aggregation, field: "Amount", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public void Name_IsSetFromConstructor()
    {
        QueryAggregateRunner<TestItem> runner = new("Granit.Test.OpenItems", new TestItemSource(_db));
        runner.Name.ShouldBe("Granit.Test.OpenItems");
    }

    private sealed class TestItem
    {
        public Guid Id { get; set; }
        public bool IsOpen { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestItem> Items => Set<TestItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestItem>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.IsOpen);
            });
        }
    }

    private sealed class TestItemSource(TestDbContext db) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => db.Items.AsQueryable();
    }
}

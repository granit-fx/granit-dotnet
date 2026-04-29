using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
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
    private readonly IQueryEngine<TestItem> _engine = NSubstitute.Substitute.For<IQueryEngine<TestItem>>();

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

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(3m);
    }

    [Fact]
    public async Task ExecuteAsync_Count_OverEmptyTable_ReturnsZero_NotNull()
    {
        // Locked semantics shared with MetricExecutor (story #1374): Count over
        // an empty set is always 0, never null.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverDecimalField_ReturnsTotal()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(60m); // 10 + 20 + 30
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverEmptySet_ReturnsZero_NotNull()
    {
        // Sum-of-empty is 0 (mathematical identity). Never null.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_Avg_OverDecimalField_ReturnsMean()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Avg, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(20m); // (10 + 20 + 30) / 3
    }

    [Fact]
    public async Task ExecuteAsync_Avg_OverEmptySet_ReturnsNull()
    {
        // Avg-of-empty is undefined (division by zero). Never zero.
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Avg, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(AggregateFunction.Min, 10)]
    [InlineData(AggregateFunction.Max, 30)]
    public async Task ExecuteAsync_MinMax_OverDecimalField_ReturnsExtremum(AggregateFunction aggregation, int expected)
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            aggregation, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_MinMax_OverEmptySet_ReturnsNull(AggregateFunction aggregation)
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            aggregation, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverIntField_RoundsTripsViaInt()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Quantity", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(6m); // 1 + 2 + 3
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverDoubleField_RoundsTripsViaDouble()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Score", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(6m); // 1.0 + 2.0 + 3.0
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

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Bonus", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownField_Throws()
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: "NotAColumn", dashboardFilters: null, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
        ex.Message.ShouldContain(nameof(TestItem));
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedFieldType_Throws()
    {
        // Aggregating Sum over a string column makes no sense — surface a clear
        // error so the dashboard renderer's per-widget isolation surfaces it
        // as Error (config bug, not data bug).
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: "Label", dashboardFilters: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_NonCountAggregation_WithoutField_Throws()
    {
        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                AggregateFunction.Sum, field: null, dashboardFilters: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_FieldName_IsCaseInsensitive()
    {
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new("Test.Items", new TestItemSource(_db), _engine, new TestQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(60m);
    }

    [Fact]
    public async Task ExecuteAsync_FieldWithCurrencyDeclaration_PropagatesCurrencyCode()
    {
        // CurrencyAwareQueryDefinition declares Amount with .Currency("EUR").
        // Sum/Avg/Min/Max over it must surface "EUR" on the result so the
        // evaluator can promote the snapshot to MetricValueKind.Currency.
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(_db), _engine, new CurrencyAwareQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(60m);
        result.CurrencyCode.ShouldBe("EUR");
    }

    [Fact]
    public async Task ExecuteAsync_FieldWithoutCurrencyDeclaration_PropagatesNullCurrencyCode()
    {
        // Quantity has no .Currency(...) — the result's CurrencyCode is null,
        // so the evaluator picks MetricValueKind.Number, not Currency.
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(_db), _engine, new CurrencyAwareQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Quantity", dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(6m);
        result.CurrencyCode.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Count_NeverCarriesCurrency_EvenIfDefinitionHasCurrencyColumns()
    {
        // Count is a row count, not a monetary aggregation — no currency
        // applies even when the definition declares Amount as EUR.
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(_db), _engine, new CurrencyAwareQueryDefinition());

        QueryAggregateRunnerResult result = await runner.ExecuteAsync(
            AggregateFunction.Count, field: null, dashboardFilters: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(3m);
        result.CurrencyCode.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DifferentCurrenciesPerColumn_AreIndependent()
    {
        // Amount → EUR, Bonus → USD on the same definition. Each aggregation
        // surfaces its own column's currency.
        SeedThree();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        QueryAggregateRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(_db), _engine, new CurrencyAwareQueryDefinition());

        QueryAggregateRunnerResult amount = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Amount", dashboardFilters: null, TestContext.Current.CancellationToken);
        QueryAggregateRunnerResult bonus = await runner.ExecuteAsync(
            AggregateFunction.Sum, field: "Bonus", dashboardFilters: null, TestContext.Current.CancellationToken);

        amount.CurrencyCode.ShouldBe("EUR");
        bonus.CurrencyCode.ShouldBe("USD");
    }

    private void SeedThree()
    {
        _db.Items.AddRange(
            new TestItem { Id = Guid.NewGuid(), Amount = 10m, Quantity = 1, Score = 1.0d, Label = "a", Bonus = 5m },
            new TestItem { Id = Guid.NewGuid(), Amount = 20m, Quantity = 2, Score = 2.0d, Label = "b", Bonus = 10m },
            new TestItem { Id = Guid.NewGuid(), Amount = 30m, Quantity = 3, Score = 3.0d, Label = "c", Bonus = 15m });
    }

    public sealed class TestItem
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public double Score { get; set; }
        public string Label { get; set; } = string.Empty;
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
                b.Property(x => x.Amount);
                b.Property(x => x.Quantity);
                b.Property(x => x.Score);
                b.Property(x => x.Label);
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
                .Column(x => x.Amount, c => c.Label("Amount"))
                .Column(x => x.Quantity, c => c.Label("Quantity"))
                .Column(x => x.Score, c => c.Label("Score"))
                .Column(x => x.Label, c => c.Label("Label"))
                .Column(x => x.Bonus, c => c.Label("Bonus"));
        }
    }

    public sealed class CurrencyAwareQueryDefinition : QueryDefinition<TestItem>
    {
        public override string Name => "Test.Items";

        protected override void Configure(QueryDefinitionBuilder<TestItem> builder)
        {
            builder
                .Column(x => x.Amount, c => c.Label("Amount").Currency("EUR"))
                .Column(x => x.Quantity, c => c.Label("Quantity"))
                .Column(x => x.Score, c => c.Label("Score"))
                .Column(x => x.Label, c => c.Label("Label"))
                .Column(x => x.Bonus, c => c.Label("Bonus").Currency("USD"));
        }
    }
}

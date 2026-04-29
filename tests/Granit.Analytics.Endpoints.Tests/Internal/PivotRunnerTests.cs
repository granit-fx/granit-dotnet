using System.Runtime.CompilerServices;
using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="PivotRunner{TEntity}"/> — substitute the
/// <see cref="IQueryEngine{TEntity}.ExecuteStreamAsync"/> path with NSubstitute
/// so the streaming + in-memory pivot logic is exercised against a known
/// dataset, independent of EF Core translation. The QueryEngine pipeline
/// already has its own SQLite suite covering filter-pipeline application.
/// </summary>
public sealed class PivotRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_OneRowAxis_OneColumnAxis_Sum_ProducesOneCellPerCombo()
    {
        // 4 rows: (Open, EU, 10), (Open, US, 20), (Paid, EU, 30), (Open, EU, 5).
        // Pivot by row=Status, col=Region, Sum(Amount):
        //   (Open, EU) → 15, (Open, US) → 20, (Paid, EU) → 30.
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", Amount = 10m },
            new() { Status = "Open", Region = "US", Amount = 20m },
            new() { Status = "Paid", Region = "EU", Amount = 30m },
            new() { Status = "Open", Region = "EU", Amount = 5m },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "Amount",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        Dictionary<(string row, string col), decimal?> byCell =
            result.Cells.ToDictionary(c => (c.RowKeys[0], c.ColumnKeys[0]), c => c.Value);

        byCell[("Open", "EU")].ShouldBe(15m);
        byCell[("Open", "US")].ShouldBe(20m);
        byCell[("Paid", "EU")].ShouldBe(30m);
    }

    [Fact]
    public async Task ExecuteAsync_NoColumnAxis_DegradesToOneBucketPerRowTuple()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", Amount = 10m },
            new() { Status = "Open", Region = "US", Amount = 20m },
            new() { Status = "Paid", Region = "EU", Amount = 30m },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: [],
            valueField: "Amount",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        var byRow = result.Cells.ToDictionary(c => c.RowKeys[0], c => c.Value);
        byRow["Open"].ShouldBe(30m);
        byRow["Paid"].ShouldBe(30m);

        result.Cells[0].ColumnKeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleRowFields_KeysTuple_ReflectsAllDimensions()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", Year = 2025, Amount = 10m },
            new() { Status = "Open", Region = "EU", Year = 2026, Amount = 20m },
            new() { Status = "Open", Region = "EU", Year = 2025, Amount = 5m },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status", "Region"],
            columnFields: ["Year"],
            valueField: "Amount",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // Cells keyed on (Status, Region, Year)
        var byKey =
            result.Cells.ToDictionary(c => $"{c.RowKeys[0]}|{c.RowKeys[1]}|{c.ColumnKeys[0]}", c => c.Value);

        byKey["Open|EU|2025"].ShouldBe(15m);
        byKey["Open|EU|2026"].ShouldBe(20m);
    }

    [Fact]
    public async Task ExecuteAsync_Count_IgnoresValueField_AndReturnsRowCountPerCell()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU" },
            new() { Status = "Open", Region = "EU" },
            new() { Status = "Open", Region = "US" },
            new() { Status = "Paid", Region = "EU" },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: null,
            aggregation: AggregateFunction.Count,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        Dictionary<(string row, string col), decimal?> byCell =
            result.Cells.ToDictionary(c => (c.RowKeys[0], c.ColumnKeys[0]), c => c.Value);

        byCell[("Open", "EU")].ShouldBe(2m);
        byCell[("Open", "US")].ShouldBe(1m);
        byCell[("Paid", "EU")].ShouldBe(1m);
    }

    [Theory]
    [InlineData(AggregateFunction.Avg, 15)]
    [InlineData(AggregateFunction.Min, 10)]
    [InlineData(AggregateFunction.Max, 20)]
    public async Task ExecuteAsync_AvgMinMax_OverPopulatedCell_ReturnsExpectedValue(
        AggregateFunction aggregation, int expected)
    {
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", Amount = 10m },
            new() { Status = "Open", Region = "EU", Amount = 20m },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "Amount",
            aggregation: aggregation,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Cells[0].Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_AvgMinMax_OverAllNullCell_ReturnsNull(AggregateFunction aggregation)
    {
        // Cell exists (the row tuple appears) but every value is null.
        // Locked semantics from #1374: Avg/Min/Max → null on the cell value.
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", BonusNullable = null },
            new() { Status = "Open", Region = "EU", BonusNullable = null },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "BonusNullable",
            aggregation: aggregation,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Cells[0].Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Sum_OverAllNullCell_ReturnsZero_NotNull()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Region = "EU", BonusNullable = null },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "BonusNullable",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Cells[0].Value.ShouldBe(0m);
    }

    [Fact]
    public async Task ExecuteAsync_NullPropertyValue_SurfacesAsNullSentinelKey()
    {
        TestItem[] items =
        [
            new() { Status = null!, Region = "EU", Amount = 10m },
            new() { Status = "Open", Region = "EU", Amount = 20m },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "Amount",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        var byRow = result.Cells.ToDictionary(c => c.RowKeys[0], c => c.Value);
        byRow.ShouldContainKey("(null)");
        byRow["(null)"].ShouldBe(10m);
        byRow["Open"].ShouldBe(20m);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRowFields_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                rowFields: [],
                columnFields: ["Region"],
                valueField: "Amount",
                aggregation: AggregateFunction.Sum,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownRowField_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                rowFields: ["NotAField"],
                columnFields: [],
                valueField: "Amount",
                aggregation: AggregateFunction.Sum,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAField");
    }

    [Fact]
    public async Task ExecuteAsync_NonCount_WithoutValueField_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                rowFields: ["Status"],
                columnFields: [],
                valueField: null,
                aggregation: AggregateFunction.Sum,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedValueFieldType_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                rowFields: ["Status"],
                columnFields: [],
                valueField: "Region", // string field → unsupported
                aggregation: AggregateFunction.Sum,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_DashboardFilter_PassedAsQueryRequestFilter()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await runner.ExecuteAsync(
            rowFields: ["Status"],
            columnFields: ["Region"],
            valueField: "Amount",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: new Dictionary<string, string> { ["Status"] = "Open" },
            TestContext.Current.CancellationToken);

        engine.Received(1).ExecuteStreamAsync(
            Arg.Any<IQueryable<TestItem>>(),
            Arg.Is<QueryRequest>(r => r.Filter != null && r.Filter["Status.eq"] == "Open"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Name_IsSetFromConstructor()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        PivotRunner<TestItem> runner = new("Granit.Test.Items", new TestItemSource([]), engine);
        runner.Name.ShouldBe("Granit.Test.Items");
    }

    private static IQueryEngine<TestItem> ConfigureStream(IReadOnlyList<TestItem> items)
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteStreamAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => YieldAsync(items, default));
        return engine;
    }

    private static async IAsyncEnumerable<TestItem> YieldAsync(
        IReadOnlyList<TestItem> items, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (TestItem item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask;
    }

    public sealed class TestItem
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal Amount { get; set; }
        public decimal? BonusNullable { get; set; }
    }

    public sealed class TestItemSource(IReadOnlyList<TestItem> items) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => items.AsQueryable();
    }
}

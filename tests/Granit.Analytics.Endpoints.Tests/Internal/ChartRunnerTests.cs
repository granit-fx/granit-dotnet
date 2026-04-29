using System.Runtime.CompilerServices;
using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="ChartRunner{TEntity}"/> — substitute the
/// <see cref="IQueryEngine{TEntity}"/> with NSubstitute. Two paths exist:
/// Count delegates to <c>ExecuteGroupedAsync</c> (SQL-level grouping), and
/// Sum/Avg/Min/Max stream the entity set through <c>ExecuteStreamAsync</c>
/// then aggregate in memory. Tests pin both paths.
/// </summary>
public sealed class ChartRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_Count_DelegatesToExecuteGrouped()
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteGroupedAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new GroupedResult<TestItem>(
                [
                    new GroupEntry<TestItem> { Field = "Status", Value = "Open", Label = "Open", Count = 12 },
                    new GroupEntry<TestItem> { Field = "Status", Value = "Paid", Label = "Paid", Count = 30 },
                ],
                TotalCount: 42));

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Count,
            field: null,
            TestContext.Current.CancellationToken);

        result.Buckets.Count.ShouldBe(2);
        result.Buckets[0].Label.ShouldBe("Open");
        result.Buckets[0].Value.ShouldBe(12m);
        result.Buckets[1].Label.ShouldBe("Paid");
        result.Buckets[1].Value.ShouldBe(30m);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_StreamsAndAggregatesInMemory()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Amount = 10m },
            new() { Status = "Open", Amount = 20m },
            new() { Status = "Paid", Amount = 50m },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: "Amount",
            TestContext.Current.CancellationToken);

        var byLabel = result.Buckets.ToDictionary(b => b.Label, b => b.Value);
        byLabel["Open"].ShouldBe(30m);
        byLabel["Paid"].ShouldBe(50m);
    }

    [Fact]
    public async Task ExecuteAsync_Sum_EmptyGroup_ReturnsZero_NotNull()
    {
        // Sum-of-empty is 0 (mathematical identity), matches MetricExecutor.
        // This test guards the case where a group has rows but all values
        // are null — the bucket still surfaces with Value=0.
        TestItem[] items =
        [
            new() { Status = "Open", BonusNullable = null },
            new() { Status = "Open", BonusNullable = null },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: "BonusNullable",
            TestContext.Current.CancellationToken);

        result.Buckets[0].Value.ShouldBe(0m);
    }

    [Theory]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_AvgMinMax_EmptyGroup_ReturnsNull(AggregateFunction aggregation)
    {
        // Avg / Min / Max over a group with no usable values surface as null
        // — locked semantics shared with MetricExecutor #1374. The frontend
        // renders "—" for that data point.
        TestItem[] items =
        [
            new() { Status = "Open", BonusNullable = null },
            new() { Status = "Open", BonusNullable = null },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: aggregation,
            field: "BonusNullable",
            TestContext.Current.CancellationToken);

        result.Buckets[0].Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Avg_PopulatedGroup_ReturnsMean()
    {
        TestItem[] items =
        [
            new() { Status = "Open", Amount = 10m },
            new() { Status = "Open", Amount = 30m },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Avg,
            field: "Amount",
            TestContext.Current.CancellationToken);

        result.Buckets[0].Value.ShouldBe(20m);
    }

    [Theory]
    [InlineData(AggregateFunction.Min, 10)]
    [InlineData(AggregateFunction.Max, 30)]
    public async Task ExecuteAsync_MinMax_PopulatedGroup_ReturnsExtremum(AggregateFunction aggregation, int expected)
    {
        TestItem[] items =
        [
            new() { Status = "Open", Amount = 10m },
            new() { Status = "Open", Amount = 20m },
            new() { Status = "Open", Amount = 30m },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: aggregation,
            field: "Amount",
            TestContext.Current.CancellationToken);

        result.Buckets[0].Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Quantity", typeof(int))]
    [InlineData("BigQuantity", typeof(long))]
    [InlineData("Amount", typeof(decimal))]
    [InlineData("Score", typeof(double))]
    public async Task ExecuteAsync_Sum_AcceptsAllSupportedPrimitiveTypes(string field, Type _)
    {
        TestItem[] items = [new() { Status = "X", Amount = 1m, Quantity = 1, BigQuantity = 1L, Score = 1.0 }];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        ChartRunnerResult result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Sum,
            field: field,
            TestContext.Current.CancellationToken);

        result.Buckets[0].Value.ShouldBe(1m);
    }

    [Fact]
    public async Task ExecuteAsync_NullGroupKey_SurfacesAsNullSentinel()
    {
        TestItem[] items =
        [
            new() { Status = null!, Amount = 10m },
            new() { Status = "Open", Amount = 20m },
        ];

        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, items);

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

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
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, []);
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

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
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, []);
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

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
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ConfigureStream(engine, []);
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                groupBy: "Status",
                aggregation: AggregateFunction.Sum,
                field: "Status",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_NonCountAggregation_WithoutField_Throws()
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

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
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

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
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ChartRunner<TestItem> runner = new("Granit.Test.Items", new TestItemSource([]), engine);

        runner.Name.ShouldBe("Granit.Test.Items");
    }

    private static void ConfigureStream(IQueryEngine<TestItem> engine, IReadOnlyList<TestItem> items) =>
        engine.ExecuteStreamAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => YieldAsync(items, default));

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
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public long BigQuantity { get; set; }
        public double Score { get; set; }
        public decimal? BonusNullable { get; set; }
    }

    public sealed class TestItemSource(IReadOnlyList<TestItem> items) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => items.AsQueryable();
    }
}

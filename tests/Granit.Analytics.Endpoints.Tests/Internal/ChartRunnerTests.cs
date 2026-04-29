using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="ChartRunner{TEntity}"/> — substitute the
/// <see cref="IQueryEngine{TEntity}"/> with NSubstitute so the runner's
/// dispatch + bucket projection are exercised against a known
/// <see cref="GroupedResult{T}"/>, independent of EF Core translation.
/// The QueryEngine's grouping pipeline already has its own SQLite
/// integration suite.
/// </summary>
public sealed class ChartRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_Count_ProjectsGroupedResultIntoBuckets()
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

        ChartRunnerResult? result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Count,
            field: null,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Buckets.Count.ShouldBe(2);
        result.Buckets[0].Label.ShouldBe("Open");
        result.Buckets[0].Value.ShouldBe(12m);
        result.Buckets[1].Label.ShouldBe("Paid");
        result.Buckets[1].Value.ShouldBe(30m);
    }

    [Fact]
    public async Task ExecuteAsync_PassesGroupByFieldOnTheRequest()
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteGroupedAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new GroupedResult<TestItem>([], 0));

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Count,
            field: null,
            TestContext.Current.CancellationToken);

        await engine.Received(1).ExecuteGroupedAsync(
            Arg.Any<IQueryable<TestItem>>(),
            Arg.Is<QueryRequest>(r => r.GroupBy == "Status"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyResult_ReturnsEmptyBucketList()
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteGroupedAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new GroupedResult<TestItem>([], 0));

        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ChartRunnerResult? result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: AggregateFunction.Count,
            field: null,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Buckets.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task ExecuteAsync_NonCountAggregations_ReturnNull_UntilFollowUpSliceLands(AggregateFunction aggregation)
    {
        // B3-5 ships Count only — the runner returns null for Sum / Avg / Min /
        // Max so the caller (ChartWidgetInstanceRenderer) maps onto a dedicated
        // Widget:Unavailable.* reason.
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        ChartRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ChartRunnerResult? result = await runner.ExecuteAsync(
            groupBy: "Status",
            aggregation: aggregation,
            field: "Amount",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();

        // The QueryEngine must NOT have been called for an unsupported
        // aggregation — short-circuit before issuing the SQL group-by.
        await engine.DidNotReceive().ExecuteGroupedAsync(
            Arg.Any<IQueryable<TestItem>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>());
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

    public sealed class TestItem
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class TestItemSource(IReadOnlyList<TestItem> items) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => items.AsQueryable();
    }
}

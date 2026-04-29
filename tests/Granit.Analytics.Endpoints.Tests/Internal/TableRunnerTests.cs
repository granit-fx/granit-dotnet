using System.Text.Json;
using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="TableRunner{TEntity}"/> — substitute the
/// <see cref="IQueryEngine{TEntity}"/> with NSubstitute so the projection
/// logic is exercised against a known result set, independent of EF Core
/// translation. The runner's job is to reshape entities into the wire's
/// camelCase JSON rows; the QueryEngine pipeline already has its own SQLite
/// integration tests.
/// </summary>
public sealed class TableRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_NullVisibleColumns_ReturnsEveryDeclaredColumn_InOrder()
    {
        TableRunner<TestItem> runner = BuildRunner(
            [new TestItem(Guid.NewGuid(), "Alice", 100m), new TestItem(Guid.NewGuid(), "Bob", 200m)],
            totalCount: 2);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: null,
            pageSize: 10,
            TestContext.Current.CancellationToken);

        result.Columns.Select(c => c.Name).ShouldBe(["name", "amount"]);
        result.Columns[0].LabelLocalizationKey.ShouldBe("Column:Name");
        result.Columns[1].LabelLocalizationKey.ShouldBe("Column:Amount");
        result.Rows.Count.ShouldBe(2);
        result.TotalRowCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_SubsetVisibleColumns_OnlyReturnsThose_InRequestOrder()
    {
        TableRunner<TestItem> runner = BuildRunner(
            [new TestItem(Guid.NewGuid(), "Alice", 100m)],
            totalCount: 1);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: ["Amount", "Name"],
            pageSize: 10,
            TestContext.Current.CancellationToken);

        result.Columns.Select(c => c.Name).ShouldBe(["amount", "name"]);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownVisibleColumn_Throws()
    {
        TableRunner<TestItem> runner = BuildRunner([], totalCount: 0);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                visibleColumns: ["NotAColumn"],
                pageSize: 10,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
        ex.Message.ShouldContain("Test.Items");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyResultSet_ReturnsEmptyRowsAndZeroTotal()
    {
        TableRunner<TestItem> runner = BuildRunner([], totalCount: 0);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: null,
            pageSize: 10,
            TestContext.Current.CancellationToken);

        result.Rows.ShouldBeEmpty();
        result.TotalRowCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_PaginatedResult_PreservesTotalRowCount()
    {
        // The runner shows 5 rows but the query has 142 — the wire envelope
        // surfaces TotalRowCount=142 so the frontend can render "showing 5 of 142".
        var id = Guid.NewGuid();
        TableRunner<TestItem> runner = BuildRunner(
            [new TestItem(id, "Alice", 100m)],
            totalCount: 142);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: null,
            pageSize: 5,
            TestContext.Current.CancellationToken);

        result.Rows.Count.ShouldBe(1);
        result.TotalRowCount.ShouldBe(142);
    }

    [Fact]
    public async Task ExecuteAsync_RowKeysAreCamelCase()
    {
        // ADR-039 §6.1 — wire convention is camelCase keys + PascalCase enums.
        // The Name property on the entity must serialise as "name", not "Name".
        TableRunner<TestItem> runner = BuildRunner(
            [new TestItem(Guid.NewGuid(), "Alice", 100m)],
            totalCount: 1);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: null,
            pageSize: 10,
            TestContext.Current.CancellationToken);

        string raw = result.Rows[0].GetRawText();
        raw.Contains("\"name\":\"Alice\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"amount\":100", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"Name\"", StringComparison.Ordinal).ShouldBeFalse(raw);
    }

    [Fact]
    public async Task ExecuteAsync_NullValues_SerialiseAsJsonNull()
    {
        TableRunner<TestItem> runner = BuildRunner(
            [new TestItem(Guid.NewGuid(), Name: null, Amount: 0m)],
            totalCount: 1);

        TableRunnerResult result = await runner.ExecuteAsync(
            visibleColumns: ["Name"],
            pageSize: 10,
            TestContext.Current.CancellationToken);

        JsonElement nameValue = result.Rows[0].GetProperty("name");
        nameValue.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ExecuteAsync_PassesPageSizeOnTheRequest()
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteAsync(Arg.Any<IQueryable<TestItem>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TestItem>([], TotalCount: 0, HasMore: false));

        TableRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource([]), engine, new TestQueryDefinition());

        await runner.ExecuteAsync(
            visibleColumns: null,
            pageSize: 7,
            TestContext.Current.CancellationToken);

        await engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<TestItem>>(),
            Arg.Is<QueryRequest>(r => r.PageSize == 7 && r.Page == 1),
            Arg.Any<CancellationToken>());
    }

    private static TableRunner<TestItem> BuildRunner(IReadOnlyList<TestItem> entities, int totalCount)
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteAsync(Arg.Any<IQueryable<TestItem>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TestItem>(entities, TotalCount: totalCount, HasMore: false));

        return new TableRunner<TestItem>(
            name: "Test.Items",
            source: new TestItemSource(entities),
            engine: engine,
            definition: new TestQueryDefinition());
    }

    public sealed record TestItem(Guid Id, string? Name, decimal Amount);

    public sealed class TestItemSource(IReadOnlyList<TestItem> items) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => items.AsQueryable();
    }

    public sealed class TestQueryDefinition : QueryDefinition<TestItem>
    {
        public override string Name => "Test.Items";

        protected override void Configure(QueryDefinitionBuilder<TestItem> builder)
        {
            builder
                .Column(x => x.Name, c => c.Label("Name").LabelKey("Column:Name"))
                .Column(x => x.Amount, c => c.Label("Amount").LabelKey("Column:Amount"));
        }
    }
}

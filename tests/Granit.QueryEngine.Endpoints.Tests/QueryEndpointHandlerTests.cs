using Granit.QueryEngine.Endpoints.Dtos;
using Granit.QueryEngine.Endpoints.Internal;
using Granit.QueryEngine.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests;

public sealed class QueryEndpointHandlerTests
{
    public sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task QueryAsync_WithoutGroupBy_ReturnsPagedResult()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        var pagedResult = new PagedResult<TestEntity>(
            [new TestEntity { Name = "A" }], 1, HasMore: false);
        engine.ExecuteAsync(Arg.Any<IQueryable<TestEntity>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        BindableQueryRequest request = CreateBindableRequest(new QueryRequest());
        IQueryable<TestEntity> source = Array.Empty<TestEntity>().AsQueryable();

        IResult result = await QueryEndpointHandler.QueryAsync(
            engine, request, source, TestContext.Current.CancellationToken);

        Ok<PagedResult<TestEntity>> okResult = result.ShouldBeOfType<Ok<PagedResult<TestEntity>>>();
        okResult.Value.ShouldBe(pagedResult);
    }

    [Fact]
    public async Task QueryAsync_WithGroupBy_ReturnsGroupedResult()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        var groupedResult = new GroupedResult<TestEntity>([], 0);
        engine.ExecuteGroupedAsync(Arg.Any<IQueryable<TestEntity>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(groupedResult);

        var queryRequest = new QueryRequest { GroupBy = "Name" };
        BindableQueryRequest request = CreateBindableRequest(queryRequest);
        IQueryable<TestEntity> source = Array.Empty<TestEntity>().AsQueryable();

        IResult result = await QueryEndpointHandler.QueryAsync(
            engine, request, source, TestContext.Current.CancellationToken);

        Ok<GroupedResult<TestEntity>> okResult = result.ShouldBeOfType<Ok<GroupedResult<TestEntity>>>();
        okResult.Value.ShouldBe(groupedResult);
    }

    [Fact]
    public async Task QueryAsync_EmptyGroupBy_ReturnsPagedResult()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        var pagedResult = new PagedResult<TestEntity>([], 0, HasMore: false);
        engine.ExecuteAsync(Arg.Any<IQueryable<TestEntity>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var queryRequest = new QueryRequest { GroupBy = "  " };
        BindableQueryRequest request = CreateBindableRequest(queryRequest);
        IQueryable<TestEntity> source = Array.Empty<TestEntity>().AsQueryable();

        IResult result = await QueryEndpointHandler.QueryAsync(
            engine, request, source, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<PagedResult<TestEntity>>>();
    }

    [Fact]
    public void GetMetadata_ReturnsEngineMetadata()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata().Returns(metadata);

        Ok<QueryMetadata> result = QueryEndpointHandler.GetMetadata(engine);

        result.Value.ShouldBe(metadata);
    }

    private static QueryMetadata CreateMetadata() =>
        new()
        {
            Columns = [],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
        };

    private static BindableQueryRequest CreateBindableRequest(QueryRequest queryRequest)
    {
        System.Reflection.ConstructorInfo? ctor = typeof(BindableQueryRequest).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(QueryRequest)]);
        return (BindableQueryRequest)ctor!.Invoke([queryRequest]);
    }
}

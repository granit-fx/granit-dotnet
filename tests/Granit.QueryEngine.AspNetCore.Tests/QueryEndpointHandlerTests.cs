using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Granit.QueryEngine.Meta;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests;

public sealed class QueryEndpointHandlerTests
{
    public sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    // -------------------------------------------------------------------------
    // QueryAsync
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // GetMetadataAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMetadataAsync_WithSubClaim_UsesSubAsUserId()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), "user-42", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-42"),
        ]));

        Ok<QueryMetadata> result = await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe(metadata);
        await savedViewStore.Received(1).GetListAsync("TestEntity", "user-42", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMetadataAsync_WithNameIdentifierClaim_UsesNameIdentifier()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), "name-id-user", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "name-id-user"),
            new Claim("sub", "sub-user"),
        ]));

        await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        await savedViewStore.Received(1).GetListAsync("TestEntity", "name-id-user", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMetadataAsync_WithNoUserClaims_UsesEmptyString()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), string.Empty, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity());

        await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        await savedViewStore.Received(1).GetListAsync("TestEntity", string.Empty, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMetadataAsync_WithTenant_PassesTenantId()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        var tenantId = Guid.NewGuid();
        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), Arg.Any<string>(), tenantId, Arg.Any<CancellationToken>())
            .Returns([]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
        ]));

        await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        await savedViewStore.Received(1).GetListAsync("TestEntity", "user-1", tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMetadataAsync_MapsSavedViewsToSummaries()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        var viewId = Guid.NewGuid();
        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SavedView>)
            [
                new()
                {
                    Id = viewId,
                    EntityType = "TestEntity",
                    Name = "My View",
                    UserId = "user-1",
                    IsShared = true,
                    IsDefault = false,
                },
            ]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")]));

        await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        engine.Received(1).GetMetadata(Arg.Is<IReadOnlyList<SavedViewSummary>>(list =>
            list.Count == 1
            && list[0].Id == viewId
            && list[0].Name == "My View"
            && list[0].IsShared
            && !list[0].IsDefault));
    }

    [Fact]
    public async Task GetMetadataAsync_NullSavedViewStore_ReturnsEmptySavedViews()
    {
        IQueryEngine<TestEntity> engine = Substitute.For<IQueryEngine<TestEntity>>();
        QueryDefinition<TestEntity> definition = CreateDefinition();
        QueryMetadata metadata = CreateMetadata();
        engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>()).Returns(metadata);

        ISavedViewStoreReader savedViewStore = Substitute.For<ISavedViewStoreReader>();
        savedViewStore.GetListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Throws(new NotImplementedException());

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")]));

        Ok<QueryMetadata> result = await QueryEndpointHandler.GetMetadataAsync(
            engine, savedViewStore, definition, tenant, user,
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe(metadata);
        engine.Received(1).GetMetadata(Arg.Is<IReadOnlyList<SavedViewSummary>>(list => list.Count == 0));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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
        // Use reflection to create BindableQueryRequest since the constructor is private.
        System.Reflection.ConstructorInfo? ctor = typeof(BindableQueryRequest).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(QueryRequest)]);
        return (BindableQueryRequest)ctor!.Invoke([queryRequest]);
    }

    private static QueryDefinition<TestEntity> CreateDefinition() =>
        new InlineQueryDefinition();

    private sealed class InlineQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "TestEntity";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder)
        {
            // Minimal definition for testing.
        }
    }
}

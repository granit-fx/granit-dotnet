using System.Net;
using System.Net.Http.Json;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.QueryEngine.Meta;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Integration;

public sealed class QueryEndpointIntegrationTests : IAsyncDisposable
{
    private const string Prefix = "/api/products";

    private readonly IQueryEngine<TestProduct> _engine = Substitute.For<IQueryEngine<TestProduct>>();
    private readonly ISavedViewStoreReader _savedViewStoreReader = Substitute.For<ISavedViewStoreReader>();
    private readonly ISavedViewStoreWriter _savedViewStoreWriter = Substitute.For<ISavedViewStoreWriter>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public QueryEndpointIntegrationTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        // Register mocks
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton(_savedViewStoreReader);
        builder.Services.AddSingleton(_savedViewStoreWriter);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());
        builder.Services.AddSingleton(Substitute.For<IClock>());

        _app = builder.Build();

        // Map query endpoints with a fake source
        _app.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            Prefix);

        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient("user");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET / ──────────────────────────────────────────────────────

    [Fact]
    public async Task Query_returns_paged_result()
    {
        PagedResult<TestProduct> expected = new(
            [new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000 }],
            1, HasMore: false);

        _engine.ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expected);

        HttpResponseMessage response = await _authClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagedResult<TestProduct>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<TestProduct>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Laptop");
    }

    [Fact]
    public async Task Query_passes_parsed_filter_to_engine()
    {
        _engine.ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TestProduct>([], 0, HasMore: false));

        await _authClient.GetAsync(
            $"{Prefix}?page=2&pageSize=10&search=laptop&filter[Price.gte]=500",
            TestContext.Current.CancellationToken);

        await _engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Is<QueryRequest>(r =>
                r.Page == 2 &&
                r.PageSize == 10 &&
                r.Search == "laptop" &&
                r.Filter != null &&
                r.Filter.ContainsKey("Price.gte")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Query_with_groupBy_calls_ExecuteGroupedAsync()
    {
        GroupedResult<TestProduct> expected = new(
            [new GroupEntry<TestProduct> { Field = "Category", Value = "Electronics", Label = "Electronics", Count = 3 }],
            3);

        _engine.ExecuteGroupedAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expected);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}?groupBy=Category", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _engine.Received(1).ExecuteGroupedAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Is<QueryRequest>(r => r.GroupBy == "Category"),
            Arg.Any<CancellationToken>());
    }

    // ── GET /meta ──────────────────────────────────────────────────

    [Fact]
    public async Task Meta_returns_query_metadata()
    {
        QueryMetadata metadata = new()
        {
            Columns = [new ColumnDefinition("Name", "Name", "String", 0, true, true, true, null)],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
            DefaultSort = "-Price",
        };

        _engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>())
            .Returns(metadata);

        _savedViewStoreReader.GetListAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/meta", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        QueryMetadata? result = await response.Content
            .ReadFromJsonAsync<QueryMetadata>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Columns.Count.ShouldBe(1);
        result.DefaultSort.ShouldBe("-Price");
    }

    [Fact]
    public async Task Meta_tolerates_NullSavedViewStore()
    {
        QueryMetadata metadata = new()
        {
            Columns = [],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
            DefaultSort = null,
        };

        _engine.GetMetadata(Arg.Any<IReadOnlyList<SavedViewSummary>?>())
            .Returns(metadata);

        _savedViewStoreReader.GetListAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Throws(new NotImplementedException("No store"));

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/meta", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Saved Views CRUD ───────────────────────────────────────────

    [Fact]
    public async Task SavedViews_GetList_returns_views()
    {
        _savedViewStoreReader.GetListAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Test.Products",
                    Name = "My View",
                    UserId = "test-user-id",
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "test-user-id",
                },
            ]);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/saved-views", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SavedViews_Create_returns_201()
    {
        CreateSavedViewRequest request = new()
        {
            Name = "My Filter",
            IsShared = false,
            IsDefault = false,
            FilterJson = "{\"status.eq\":\"active\"}",
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/saved-views", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        await _savedViewStoreWriter.Received(1).CreateAsync(
            Arg.Is<SavedView>(v =>
                v.Name == "My Filter" &&
                v.FilterJson == "{\"status.eq\":\"active\"}" &&
                v.UserId == "test-user-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavedViews_Update_returns_204_when_found()
    {
        var viewId = Guid.NewGuid();
        _savedViewStoreReader.GetAsync(viewId, Arg.Any<CancellationToken>())
            .Returns(new SavedView
            {
                Id = viewId,
                EntityType = "Test.Products",
                Name = "Old Name",
                UserId = "test-user-id",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test-user-id",
            });

        UpdateSavedViewRequest request = new()
        {
            Name = "New Name",
            IsShared = true,
        };

        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/saved-views/{viewId}", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _savedViewStoreWriter.Received(1).UpdateAsync(
            Arg.Is<SavedView>(v => v.Name == "New Name" && v.IsShared),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavedViews_Update_returns_404_when_not_found()
    {
        var viewId = Guid.NewGuid();
        _savedViewStoreReader.GetAsync(viewId, Arg.Any<CancellationToken>())
            .Returns((SavedView?)null);

        UpdateSavedViewRequest request = new() { Name = "Whatever" };

        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/saved-views/{viewId}", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SavedViews_Delete_returns_204()
    {
        var viewId = Guid.NewGuid();
        _savedViewStoreReader.GetAsync(viewId, Arg.Any<CancellationToken>())
            .Returns(new SavedView
            {
                Id = viewId,
                EntityType = "Test.Products",
                Name = "To Delete",
                UserId = "test-user-id",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test-user-id",
            });

        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/saved-views/{viewId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _savedViewStoreWriter.Received(1).DeleteAsync(viewId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavedViews_SetDefault_returns_204()
    {
        var viewId = Guid.NewGuid();
        _savedViewStoreReader.GetAsync(viewId, Arg.Any<CancellationToken>())
            .Returns(new SavedView
            {
                Id = viewId,
                EntityType = "Test.Products",
                Name = "Default View",
                UserId = "test-user-id",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test-user-id",
            });

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/saved-views/{viewId}/set-default", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _savedViewStoreWriter.Received(1).SetDefaultAsync(
            viewId, "test-user-id", "Test.Products", Arg.Any<CancellationToken>());
    }

    // ── Options ────────────────────────────────────────────────────

    [Fact]
    public async Task MapGranitQuery_without_meta_does_not_register_meta()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton(_savedViewStoreReader);
        builder.Services.AddSingleton(_savedViewStoreWriter);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());
        builder.Services.AddSingleton(Substitute.For<IClock>());

        _engine.ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TestProduct>([], 0, HasMore: false));

        await using WebApplication customApp = builder.Build();
        customApp.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/items",
            opts =>
            {
                opts.IncludeMetaEndpoint = false;
                opts.IncludeSavedViewEndpoints = false;
            });
        await customApp.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = customApp.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        HttpResponseMessage metaResponse = await client.GetAsync(
            "/api/items/meta", TestContext.Current.CancellationToken);
        metaResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage savedViewsResponse = await client.GetAsync(
            "/api/items/saved-views", TestContext.Current.CancellationToken);
        savedViewsResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}

// ── Test fixtures ───────────────────────────────────────────────────

public sealed class TestProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
}

public sealed class TestProductQueryDefinition : QueryDefinition<TestProduct>
{
    public override string Name => "Test.Products";

    protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
        builder
            .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
            .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
            .DefaultPageSize(20)
            .MaxPageSize(100)
            .DefaultSort("-Price");
}

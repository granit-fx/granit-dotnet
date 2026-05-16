using System.Net;
using System.Net.Http.Json;
using Granit.MultiTenancy;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.QueryEngine.Meta;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Integration;

public sealed class QueryEndpointIntegrationTests : IAsyncDisposable
{
    private const string Prefix = "/api/products";

    private readonly IQueryEngine<TestProduct> _engine = Substitute.For<IQueryEngine<TestProduct>>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public QueryEndpointIntegrationTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        _app = builder.Build();

        _app.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            Prefix);

        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient("user");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

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

        _engine.GetMetadata().Returns(metadata);

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
    public async Task MapGranitQuery_without_list_registers_meta_only()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        _engine.GetMetadata().Returns(new QueryMetadata
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
        });

        await using WebApplication customApp = builder.Build();
        customApp.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/items",
            opts => opts.IncludeListEndpoint = false);
        await customApp.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = customApp.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        HttpResponseMessage listResponse = await client.GetAsync(
            "/api/items", TestContext.Current.CancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage metaResponse = await client.GetAsync(
            "/api/items/meta", TestContext.Current.CancellationToken);
        metaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapGranitQuery_without_list_or_meta_registers_no_endpoints()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        await using WebApplication customApp = builder.Build();
        Should.NotThrow(() => customApp.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/empty",
            opts =>
            {
                opts.IncludeListEndpoint = false;
                opts.IncludeMetaEndpoint = false;
            }));
        await customApp.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = customApp.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        (await client.GetAsync("/api/empty", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/empty/meta", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapGranitQuery_without_list_allows_bespoke_GET_on_same_group()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        await using WebApplication customApp = builder.Build();

        RouteGroupBuilder group = customApp.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/bespoke",
            opts =>
            {
                opts.IncludeListEndpoint = false;
                opts.IncludeMetaEndpoint = false;
                opts.AllowAnonymous = true;
            });
        group.MapGet("/", () => TypedResults.Ok(new[] { "bespoke" }));

        await customApp.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = customApp.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/bespoke", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string[]? body = await response.Content
            .ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken);
        body.ShouldBe(["bespoke"]);
    }

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
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        _engine.ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TestProduct>([], 0, HasMore: false));

        await using WebApplication customApp = builder.Build();
        customApp.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/items",
            opts => opts.IncludeMetaEndpoint = false);
        await customApp.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = customApp.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        HttpResponseMessage metaResponse = await client.GetAsync(
            "/api/items/meta", TestContext.Current.CancellationToken);
        metaResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}

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

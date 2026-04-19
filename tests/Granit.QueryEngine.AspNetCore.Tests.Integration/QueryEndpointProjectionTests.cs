using System.Net;
using System.Net.Http.Json;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Integration;

public sealed class QueryEndpointProjectionTests : IAsyncDisposable
{
    private const string Prefix = "/api/products";

    private readonly IQueryEngine<TestProduct> _engine = Substitute.For<IQueryEngine<TestProduct>>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public QueryEndpointProjectionTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton(Substitute.For<SavedViews.ISavedViewStoreReader>());
        builder.Services.AddSingleton(Substitute.For<SavedViews.ISavedViewStoreWriter>());
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, ProjectedTestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());
        builder.Services.AddSingleton(Substitute.For<IClock>());

        _app = builder.Build();

        _app.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            Prefix);

        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = _app.GetTestClient();
        _authClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Query_with_projection_returns_projected_dto_result()
    {
        PagedResult<TestProductDto> expected = new(
            [new TestProductDto(Guid.NewGuid(), "Laptop")],
            1, HasMore: false);

        _engine.ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<System.Linq.Expressions.Expression<Func<TestProduct, TestProductDto>>>(),
            Arg.Any<CancellationToken>())
            .Returns(expected);

        HttpResponseMessage response = await _authClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagedResult<TestProductDto>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<TestProductDto>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Laptop");

        await _engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<System.Linq.Expressions.Expression<Func<TestProduct, TestProductDto>>>(),
            Arg.Any<CancellationToken>());

        await _engine.DidNotReceive().ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Query_with_projection_and_groupBy_returns_entity_grouped_result()
    {
        GroupedResult<TestProduct> expected = new(
            [new GroupEntry<TestProduct>
            {
                Field = "Category",
                Value = "Electronics",
                Label = "Electronics",
                Count = 3,
            }],
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

        await _engine.DidNotReceive().ExecuteAsync(
            Arg.Any<IQueryable<TestProduct>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<System.Linq.Expressions.Expression<Func<TestProduct, TestProductDto>>>(),
            Arg.Any<CancellationToken>());
    }
}

public sealed record TestProductDto(Guid Id, string Name);

public sealed class ProjectedTestProductQueryDefinition : QueryDefinition<TestProduct>
{
    public override string Name => "Test.Products.Projected";

    protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
        builder
            .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
            .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
            .DefaultPageSize(20)
            .MaxPageSize(100)
            .DefaultSort("-Price")
            .ProjectTo(p => new TestProductDto(p.Id, p.Name));
}

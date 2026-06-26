using System.Net;
using System.Net.Http.Json;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Internal;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>GET /catalog</c> driven through <see cref="GranitEndpointTestHost"/>.
/// Exercises the live EndpointDataSource / LinkGenerator route resolution the projection unit
/// tests cannot reach: a mapped query resolves its base path, a registered-but-unmapped query
/// surfaces a null base path, and the authenticated-only gate holds.
/// </summary>
public sealed class QueryCatalogEndpointsHttpTests
{
    private static Task<GranitEndpointTestHost> StartAsync() =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();

                // Two definitions registered; only the routed one is mapped below.
                services.AddQueryDefinition<RoutedEntity, RoutedQueryDefinition>();
                services.AddQueryDefinition<UnroutedEntity, UnroutedQueryDefinition>();
                services.TryAddSingleton<IQueryDefinitionRegistry, QueryDefinitionRegistry>();
            },
            configureEndpoints: app =>
            {
                app.MapGranitQuery<RoutedEntity>(
                    _ => Enumerable.Empty<RoutedEntity>().AsQueryable(),
                    prefix: "routed-things");
                app.MapGranitQueryCatalog();
            });

    [Fact]
    public async Task Catalog_lists_every_registered_query_ordered_by_name()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Select(e => e.Name).ShouldBe(["Test.Routed", "Test.Unrouted"]);
        body.ShouldAllBe(e => e.Label == e.Name);
    }

    [Fact]
    public async Task Catalog_resolves_the_base_path_of_a_mapped_query()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse routed = body!.Single(e => e.Name == "Test.Routed");
        routed.BasePath.ShouldBe("/routed-things");
    }

    [Fact]
    public async Task Catalog_emits_null_base_path_for_a_registered_but_unmapped_query()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse unrouted = body!.Single(e => e.Name == "Test.Unrouted");
        unrouted.BasePath.ShouldBeNull();
    }

    [Fact]
    public async Task Catalog_is_unauthorized_for_an_anonymous_caller()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        HttpResponseMessage response = await host.CreateAnonymousClient()
            .GetAsync("/catalog", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class RoutedEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class UnroutedEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RoutedQueryDefinition : QueryDefinition<RoutedEntity>
    {
        public override string Name => "Test.Routed";

        protected override void Configure(QueryDefinitionBuilder<RoutedEntity> builder) =>
            builder.Column(e => e.Name);
    }

    private sealed class UnroutedQueryDefinition : QueryDefinition<UnroutedEntity>
    {
        public override string Name => "Test.Unrouted";

        protected override void Configure(QueryDefinitionBuilder<UnroutedEntity> builder) =>
            builder.Column(e => e.Name);
    }
}

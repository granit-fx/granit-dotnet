using System.Net;
using System.Net.Http.Json;
using Granit.Entities;
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
/// Exercises the live EndpointDataSource / LinkGenerator route resolution and the label-key
/// resolution the projection unit tests cannot reach: a mapped query resolves its base path, a
/// registered-but-unmapped query surfaces a null base path, the label key reuses the target
/// entity's DisplayKey when that entity is registered (falling back to <c>Query:{Name}</c>), and
/// the authenticated-only gate holds.
/// </summary>
public sealed class QueryCatalogEndpointsHttpTests
{
    private static Task<GranitEndpointTestHost> StartAsync() =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();

                // Two query definitions; only the routed one is mapped below. An entity definition
                // is registered for RoutedEntity (carrying a DisplayKey) but NOT for UnroutedEntity,
                // so the routed query reuses the entity's display key and the unrouted one falls
                // back to "Query:{Name}".
                services.AddQueryDefinition<RoutedEntity, RoutedQueryDefinition>();
                services.AddQueryDefinition<UnroutedEntity, UnroutedQueryDefinition>();
                services.AddSingleton<IEntityDefinitionDescriptor>(new RoutedEntityDefinition());
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
    public async Task Catalog_reuses_the_entity_display_key_as_the_label_key()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse routed = body!.Single(e => e.Name == "Test.Routed");
        routed.LabelKey.ShouldBe("Entity:Test.Routed");
    }

    [Fact]
    public async Task Catalog_falls_back_to_the_query_label_key_when_no_entity_is_registered()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse unrouted = body!.Single(e => e.Name == "Test.Unrouted");
        unrouted.LabelKey.ShouldBe("Query:Test.Unrouted");
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

    private sealed class RoutedEntityDefinition : EntityDefinition<RoutedEntity>
    {
        public override string Name => "Test.RoutedEntity";

        protected override void Configure(EntityDefinitionBuilder<RoutedEntity> builder) =>
            builder.DisplayKey("Entity:Test.Routed");
    }
}

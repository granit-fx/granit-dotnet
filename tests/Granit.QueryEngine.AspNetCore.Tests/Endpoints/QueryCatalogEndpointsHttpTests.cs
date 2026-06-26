using System.Net;
using System.Net.Http.Json;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Internal;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Endpoints;

/// <summary>
/// HTTP-level tests for <c>GET /catalog</c> driven through <see cref="GranitEndpointTestHost"/>.
/// Exercises the live EndpointDataSource / LinkGenerator route resolution and the localized-label
/// resolution the projection unit tests cannot reach: a mapped query resolves its base path, a
/// registered-but-unmapped query surfaces a null base path, the <c>Query:{Name}</c> localization
/// key drives the label (falling back to the raw name), and the authenticated-only gate holds.
/// </summary>
public sealed class QueryCatalogEndpointsHttpTests
{
    private static Task<GranitEndpointTestHost> StartAsync() =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton<IStringLocalizerFactory>(new StubLocalizerFactory());

                // Two definitions registered; only the routed one is mapped below. The routed
                // definition declares a localization resource carrying a "Query:Test.Routed" label;
                // the unrouted one declares none (label falls back to its name).
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
    public async Task Catalog_resolves_the_label_from_the_Query_prefixed_localization_key()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse routed = body!.Single(e => e.Name == "Test.Routed");
        routed.Label.ShouldBe("Routed Things");
    }

    [Fact]
    public async Task Catalog_falls_back_to_the_name_when_no_localization_key_exists()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        List<QueryCatalogEntryResponse>? body = await host.CreateAuthenticatedClient()
            .GetFromJsonAsync<List<QueryCatalogEntryResponse>>("/catalog", TestContext.Current.CancellationToken);

        QueryCatalogEntryResponse unrouted = body!.Single(e => e.Name == "Test.Unrouted");
        unrouted.Label.ShouldBe("Test.Unrouted");
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

        public override Type LocalizationResourceType => typeof(TestQueryLabelsResource);

        protected override void Configure(QueryDefinitionBuilder<RoutedEntity> builder) =>
            builder.Column(e => e.Name);
    }

    private sealed class UnroutedQueryDefinition : QueryDefinition<UnroutedEntity>
    {
        public override string Name => "Test.Unrouted";

        protected override void Configure(QueryDefinitionBuilder<UnroutedEntity> builder) =>
            builder.Column(e => e.Name);
    }

    private sealed class TestQueryLabelsResource;

    private sealed class StubLocalizerFactory : IStringLocalizerFactory
    {
        private static readonly Dictionary<string, string> RoutedLabels =
            new() { ["Query:Test.Routed"] = "Routed Things" };

        public IStringLocalizer Create(Type resourceSource) =>
            resourceSource == typeof(TestQueryLabelsResource)
                ? new StubLocalizer(RoutedLabels)
                : new StubLocalizer(new Dictionary<string, string>());

        public IStringLocalizer Create(string baseName, string location) => new StubLocalizer(new Dictionary<string, string>());
    }

    private sealed class StubLocalizer(IReadOnlyDictionary<string, string> map) : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            map.TryGetValue(name, out string? value)
                ? new LocalizedString(name, value, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

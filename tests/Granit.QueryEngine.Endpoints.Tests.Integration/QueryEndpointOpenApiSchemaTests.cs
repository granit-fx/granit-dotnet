using System.Text.Json;
using Granit.MultiTenancy;
using Granit.QueryEngine.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests.Integration;

/// <summary>
/// Regression tests for the OpenAPI document produced by <c>MapGranitQuery</c>.
///
/// The query endpoint declares two <c>Produces&lt;&gt;</c> calls at status 200
/// (<c>PagedResult&lt;T&gt;</c> and <c>GroupedResult&lt;T&gt;</c>) and rewrites
/// the response schema to a <c>oneOf</c> of <c>$ref</c>s. ASP.NET Core's schema
/// collector dedups by status code, so historically only the second type
/// (GroupedResult) ended up in <c>components.schemas</c>, leaving the PagedResult
/// <c>$ref</c> orphan and breaking codegen clients. Both wrappers must now be
/// present in components.schemas and every <c>$ref</c> emitted by the document
/// must resolve.
/// </summary>
public sealed class QueryEndpointOpenApiSchemaTests
{
    [Fact]
    public async Task NonProjectedQuery_RegistersBothPagedAndGroupedSchemas()
    {
        await using WebApplication app = await BuildAppAsync(projected: false);

        JsonDocument doc = await FetchOpenApiAsync(app);

        JsonElement schemas = doc.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        schemas.TryGetProperty("PagedResultOfTestProduct", out _).ShouldBeTrue(
            "PagedResult<TestProduct> must be registered in components.schemas");
        schemas.TryGetProperty("GroupedResultOfTestProduct", out _).ShouldBeTrue(
            "GroupedResult<TestProduct> must be registered in components.schemas");
    }

    [Fact]
    public async Task ProjectedQuery_RegistersBothPagedAndGroupedSchemas()
    {
        await using WebApplication app = await BuildAppAsync(projected: true);

        JsonDocument doc = await FetchOpenApiAsync(app);

        JsonElement schemas = doc.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        schemas.TryGetProperty("PagedResultOfTestProductDto", out _).ShouldBeTrue(
            "PagedResult<TestProductDto> must be registered in components.schemas");
        schemas.TryGetProperty("GroupedResultOfTestProductDto", out _).ShouldBeTrue(
            "GroupedResult<TestProductDto> must be registered in components.schemas");
    }

    [Fact]
    public async Task QueryEndpoint_EmitsNoOrphanRefs()
    {
        await using WebApplication app = await BuildAppAsync(projected: true);

        JsonDocument doc = await FetchOpenApiAsync(app);

        var declared = doc.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<string> orphans = [];
        CollectOrphanRefs(doc.RootElement, declared, orphans);

        orphans.ShouldBeEmpty(
            $"every $ref must resolve in components.schemas; orphans: {string.Join(", ", orphans)}");
    }

    private static async Task<WebApplication> BuildAppAsync(bool projected)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        builder.Services.AddSingleton(Substitute.For<IQueryEngine<TestProduct>>());
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        if (projected)
        {
            builder.Services.AddSingleton<QueryDefinition<TestProduct>, ProjectedTestProductQueryDefinition>();
        }
        else
        {
            builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        }

        WebApplication app = builder.Build();
        app.MapOpenApi();
        app.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/products");

        await app.StartAsync();
        return app;
    }

    private static async Task<JsonDocument> FetchOpenApiAsync(WebApplication app)
    {
        HttpClient client = app.GetTestClient();
        HttpResponseMessage response = await client.GetAsync(
            "/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Stream stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static void CollectOrphanRefs(JsonElement node, HashSet<string> declared, List<string> orphans)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty prop in node.EnumerateObject())
                {
                    if (prop.NameEquals("$ref") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        string? value = prop.Value.GetString();
                        if (value?.StartsWith("#/components/schemas/", StringComparison.Ordinal) == true)
                        {
                            string id = value["#/components/schemas/".Length..];
                            if (!declared.Contains(id) && !orphans.Contains(id))
                            {
                                orphans.Add(id);
                            }
                        }
                    }
                    else
                    {
                        CollectOrphanRefs(prop.Value, declared, orphans);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in node.EnumerateArray())
                {
                    CollectOrphanRefs(item, declared, orphans);
                }
                break;
        }
    }
}

using Granit.Dashboards.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests;

/// <summary>
/// Locks the OpenAPI sub-tag wiring for <c>MapGranitDashboards</c> — every endpoint
/// must carry exactly one of the three documented tags
/// (<c>Dashboards - Catalogue</c>, <c>Dashboards - Instances</c>,
/// <c>Dashboards - Widgets</c>) per the route group it belongs to. CLAUDE.md
/// tagging convention: <c>&lt;Module&gt; - &lt;SubGroup&gt;</c>.
/// </summary>
public sealed class MapGranitDashboardsTests
{
    [Fact]
    public void MapGranitDashboards_WithDefaultOptions_ReturnsRouteGroup()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        RouteGroupBuilder result = app.MapGranitDashboards();

        result.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitDashboards_GroupsCatalogueImportInstancesAndWidgetsUnderDistinctTags()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        app.MapGranitDashboards();

        Dictionary<string, IReadOnlyList<string>> tagsByPath = CollectTagsByPath(app);

        AssertTag(tagsByPath, "/dashboards/catalog", "Dashboards - Catalogue");

        // Instances sub-tag covers import + list + read + edit + state transitions.
        AssertTag(tagsByPath, "/dashboards/from-definition/{name}", "Dashboards - Instances");
        AssertTag(tagsByPath, "/dashboards/", "Dashboards - Instances");
        AssertTag(tagsByPath, "/dashboards/{id:guid}", "Dashboards - Instances");
        AssertTag(tagsByPath, "/dashboards/{id:guid}/publish", "Dashboards - Instances");
        AssertTag(tagsByPath, "/dashboards/{id:guid}/archive", "Dashboards - Instances");
        AssertTag(tagsByPath, "/dashboards/{id:guid}/restore", "Dashboards - Instances");

        // Widgets sub-tag scopes the widget pool.
        AssertTag(tagsByPath, "/dashboards/{id:guid}/widgets", "Dashboards - Widgets");
        AssertTag(tagsByPath, "/dashboards/{id:guid}/widgets/{widgetId:guid}", "Dashboards - Widgets");
    }

    [Fact]
    public void MapGranitDashboards_HonoursCustomTagOverrides()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        app.MapGranitDashboards(opts =>
        {
            opts.CatalogTagName = "BI - Catalog";
            opts.InstancesTagName = "BI - Instances";
            opts.WidgetsTagName = "BI - Widgets";
        });

        HashSet<string> distinctTags = [.. CollectTagsByPath(app).Values.SelectMany(t => t)];

        distinctTags.ShouldBe(["BI - Catalog", "BI - Instances", "BI - Widgets"], ignoreOrder: true);
    }

    [Fact]
    public void MapGranitDashboards_HonoursCustomRoutePrefix()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        app.MapGranitDashboards(opts => opts.RoutePrefix = "bi");

        Dictionary<string, IReadOnlyList<string>> tagsByPath = CollectTagsByPath(app);
        AssertTag(tagsByPath, "/bi/catalog", "Dashboards - Catalogue");
    }

    private static void AssertTag(Dictionary<string, IReadOnlyList<string>> tagsByPath, string path, string expectedTag)
    {
        tagsByPath.ShouldContainKey(path,
            customMessage: $"Expected an endpoint at '{path}'. Available paths: {string.Join(", ", tagsByPath.Keys.OrderBy(k => k))}.");
        tagsByPath[path].ShouldBe([expectedTag]);
    }

    private static Dictionary<string, IReadOnlyList<string>> CollectTagsByPath(WebApplication app)
    {
        Dictionary<string, IReadOnlyList<string>> result = [];

        IEnumerable<Endpoint> endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints);

        foreach (Endpoint endpoint in endpoints)
        {
            if (endpoint is not RouteEndpoint route)
            {
                continue;
            }

            ITagsMetadata? tags = endpoint.Metadata.GetMetadata<ITagsMetadata>();
            if (tags is null)
            {
                continue;
            }

            string path = "/" + route.RoutePattern.RawText?.TrimStart('/');
            result[path] = [.. tags.Tags];
        }

        return result;
    }
}

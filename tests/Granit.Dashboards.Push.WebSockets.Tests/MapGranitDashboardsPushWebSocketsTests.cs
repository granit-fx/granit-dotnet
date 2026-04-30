using Granit.Dashboards.Push.Extensions;
using Granit.Dashboards.Push.WebSockets.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.WebSockets.Tests;

/// <summary>
/// Locks the route registration of the WebSocket sibling: path matches the
/// dashboards prefix with the <c>-ws</c> suffix, OpenAPI tag follows the
/// CLAUDE.md <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> convention, and the same
/// <c>Dashboards.Instances.Read</c> permission gates the upgrade. Without this
/// pin, a refactor that drops the auth attribute would silently open the live
/// channel to anonymous callers.
/// </summary>
public sealed class MapGranitDashboardsPushWebSocketsTests
{
    [Fact]
    public void MapGranitDashboardsPushWebSockets_RegistersStreamRoute_WithExpectedTagAndPermission()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush();
        builder.Services.AddGranitDashboardsPushWebSockets();
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPushWebSockets();

        Endpoint? endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText == "dashboards/{id:guid}/stream-ws");

        endpoint.ShouldNotBeNull(
            customMessage: "Expected GET /dashboards/{id:guid}/stream-ws to be registered.");

        ITagsMetadata? tags = endpoint!.Metadata.GetMetadata<ITagsMetadata>();
        tags.ShouldNotBeNull();
        tags!.Tags.ShouldBe(["Dashboards - Stream (WebSocket)"]);

        IAuthorizeData? authorize = endpoint.Metadata.GetMetadata<IAuthorizeData>();
        authorize.ShouldNotBeNull(
            customMessage: "Stream endpoint must require authorization — no anonymous live channel.");
        authorize!.Policy.ShouldBe("Dashboards.Instances.Read");
    }

    [Fact]
    public void MapGranitDashboardsPushWebSockets_HonoursCustomTagOverride()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush();
        builder.Services.AddGranitDashboardsPushWebSockets(opt => opt.TagName = "BI - Live (WS)");
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPushWebSockets();

        ITagsMetadata? tags = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "dashboards/{id:guid}/stream-ws")
            .Metadata.GetMetadata<ITagsMetadata>();

        tags!.Tags.ShouldBe(["BI - Live (WS)"]);
    }

    [Fact]
    public void MapGranitDashboardsPushWebSockets_HonoursCustomRoutePrefix()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush();
        builder.Services.AddGranitDashboardsPushWebSockets(opt => opt.RoutePrefix = "bi");
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPushWebSockets();

        Endpoint? endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText == "bi/{id:guid}/stream-ws");

        endpoint.ShouldNotBeNull();
    }
}

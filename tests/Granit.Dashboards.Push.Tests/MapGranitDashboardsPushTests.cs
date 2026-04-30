using Granit.Dashboards.Push.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.Tests;

/// <summary>
/// Locks the route registration of the SSE endpoint: path matches the dashboards
/// instances prefix, OpenAPI tag follows the CLAUDE.md
/// <c>&lt;Module&gt; - &lt;SubGroup&gt;</c> convention, and the
/// <c>Dashboards.Instances.Read</c> permission gates the endpoint. Without this
/// pin, a refactor that drops the auth attribute would silently open the live
/// channel to anonymous callers.
/// </summary>
public sealed class MapGranitDashboardsPushTests
{
    [Fact]
    public void MapGranitDashboardsPush_RegistersStreamRoute_WithExpectedTagAndPermission()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush();
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPush();

        Endpoint? endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText == "dashboards/{id:guid}/stream");

        endpoint.ShouldNotBeNull(
            customMessage: "Expected GET /dashboards/{id:guid}/stream to be registered.");

        ITagsMetadata? tags = endpoint!.Metadata.GetMetadata<ITagsMetadata>();
        tags.ShouldNotBeNull();
        tags!.Tags.ShouldBe(["Dashboards - Stream"]);

        IAuthorizeData? authorize = endpoint.Metadata.GetMetadata<IAuthorizeData>();
        authorize.ShouldNotBeNull(
            customMessage: "Stream endpoint must require authorization — no anonymous live channel.");
        authorize!.Policy.ShouldBe("Dashboards.Instances.Read");
    }

    [Fact]
    public void MapGranitDashboardsPush_HonoursCustomTagOverride()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush(opt => opt.TagName = "BI - Live");
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPush();

        ITagsMetadata? tags = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "dashboards/{id:guid}/stream")
            .Metadata.GetMetadata<ITagsMetadata>();

        tags!.Tags.ShouldBe(["BI - Live"]);
    }

    [Fact]
    public void MapGranitDashboardsPush_HonoursCustomRoutePrefix()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGranitDashboardsPush(opt => opt.RoutePrefix = "bi");
        WebApplication app = builder.Build();

        app.MapGranitDashboardsPush();

        Endpoint? endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText == "bi/{id:guid}/stream");

        endpoint.ShouldNotBeNull();
    }
}

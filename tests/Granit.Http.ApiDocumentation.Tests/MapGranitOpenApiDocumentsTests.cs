// =============================================================================
// Tests - MapGranitOpenApiDocuments
// =============================================================================
// Vérifie que MapGranitOpenApiDocuments mappe un endpoint /openapi/v{n}.json
// par version majeure configurée (sans UI ni gating environnement) et invoque
// le hook de convention par endpoint.
// =============================================================================

using Granit.Http.ApiDocumentation.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class MapGranitOpenApiDocumentsTests
{
    [Fact]
    public async Task MapsOneJsonEndpointPerMajorVersion()
    {
        // Arrange
        WebApplication app = BuildApp(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "2",
        });

        // Act
        app.MapGranitOpenApiDocuments();

        // Assert — endpoint data sources materialise after start
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            EndpointDataSource dataSource = app.Services.GetRequiredService<EndpointDataSource>();
            List<string> routes = [.. dataSource.Endpoints
                .OfType<RouteEndpoint>()
                .Select(e => e.RoutePattern.RawText ?? string.Empty)];

            routes.ShouldContain("/openapi/v1.json");
            routes.ShouldContain("/openapi/v2.json");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void ProductionEnvironment_StillMapsRoutes()
    {
        // Headless mapping has no environment gate — an explicit call is explicit intent.
        // (UI gating lives in the Granit.Http.ApiDocumentation.Scalar package.)
        WebApplication app = BuildApp(
            new Dictionary<string, string?> { ["Http:ApiDocumentation:MajorVersions:0"] = "1" },
            Environments.Production);

        WebApplication result = app.MapGranitOpenApiDocuments();

        result.ShouldBeSameAs(app);
    }

    [Fact]
    public void ConfigureEndpointHook_IsInvokedOncePerVersion()
    {
        // Arrange
        WebApplication app = BuildApp(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "2",
            ["Http:ApiDocumentation:MajorVersions:2"] = "3",
        });
        int invocations = 0;

        // Act
        app.MapGranitOpenApiDocuments(_ => invocations++);

        // Assert
        invocations.ShouldBe(3);
    }

    [Fact]
    public void DuplicateMajorVersions_AreMappedOnce()
    {
        // The config binder appends to the default [1] — Distinct() must dedupe.
        WebApplication app = BuildApp(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "1",
        });
        int invocations = 0;

        app.MapGranitOpenApiDocuments(_ => invocations++);

        invocations.ShouldBe(1);
    }

    private static WebApplication BuildApp(
        Dictionary<string, string?> config,
        string environmentName = "Development")
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.Configuration.AddInMemoryCollection(config);
        builder.AddGranitApiDocumentation();
        return builder.Build();
    }
}

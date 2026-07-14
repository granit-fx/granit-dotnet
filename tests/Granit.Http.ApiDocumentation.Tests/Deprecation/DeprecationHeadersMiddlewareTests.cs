// =============================================================================
// Tests - DeprecationHeadersMiddleware (end-to-end via TestServer)
// =============================================================================
// Vérifie que le middleware auto-enregistré par AddGranitApiDocumentation émet
// les en-têtes RFC 8594 (Deprecation/Sunset/Link) à partir des seules
// métadonnées DeprecatedAttribute — sans aucun câblage supplémentaire.
// =============================================================================

using Granit.Http.ApiDocumentation.Deprecation;
using Granit.Http.ApiDocumentation.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests.Deprecation;

public sealed class DeprecationHeadersMiddlewareTests
{
    [Fact]
    public async Task MetadataAlone_EmitsDeprecationHeader()
    {
        // Arrange — attribute-only path: no filter, no explicit middleware wiring.
        await using WebApplication app = BuildApp(endpoints =>
            endpoints.MapGet("/deprecated", () => "ok")
                .WithMetadata(new DeprecatedAttribute()));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/deprecated", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.Headers.GetValues("Deprecation").ShouldBe(["true"]);
        response.Headers.Contains("Sunset").ShouldBeFalse();
        response.Headers.Contains("Link").ShouldBeFalse();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SunsetDate_EmitsSunsetHeaderInRfc7231Format()
    {
        await using WebApplication app = BuildApp(endpoints =>
            endpoints.MapGet("/deprecated", () => "ok")
                .WithMetadata(new DeprecatedAttribute { SunsetDate = new DateOnly(2025, 11, 1) }));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/deprecated", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Deprecation").ShouldBe(["true"]);
        response.Headers.GetValues("Sunset").ShouldBe(["Sat, 01 Nov 2025 00:00:00 GMT"]);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Link_EmitsLinkHeaderWithSunsetRel()
    {
        await using WebApplication app = BuildApp(endpoints =>
            endpoints.MapGet("/deprecated", () => "ok")
                .WithMetadata(new DeprecatedAttribute
                {
                    Link = "https://docs.example.com/migration/v1-to-v2",
                }));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/deprecated", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Link")
            .ShouldBe(["<https://docs.example.com/migration/v1-to-v2>; rel=\"sunset\""]);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeprecatedExtension_AttachesMetadataAndEmitsAllHeaders()
    {
        // The .Deprecated(...) extension only sets metadata; the middleware does the rest.
        await using WebApplication app = BuildApp(endpoints =>
            endpoints.MapGet("/deprecated", () => "ok")
                .Deprecated(new DateOnly(2025, 6, 15), "https://docs.example.com/migration"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/deprecated", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Deprecation").ShouldBe(["true"]);
        response.Headers.GetValues("Sunset").ShouldBe(["Sun, 15 Jun 2025 00:00:00 GMT"]);
        response.Headers.GetValues("Link")
            .ShouldBe(["<https://docs.example.com/migration>; rel=\"sunset\""]);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NonDeprecatedEndpoint_EmitsNoDeprecationHeaders()
    {
        await using WebApplication app = BuildApp(endpoints =>
            endpoints.MapGet("/current", () => "ok"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/current", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.Contains("Deprecation").ShouldBeFalse();
        response.Headers.Contains("Sunset").ShouldBeFalse();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WebApplication BuildApp(Action<IEndpointRouteBuilder> mapEndpoints)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.WebHost.UseTestServer();
        builder.AddGranitApiDocumentation();

        WebApplication app = builder.Build();
        mapEndpoints(app);
        return app;
    }
}

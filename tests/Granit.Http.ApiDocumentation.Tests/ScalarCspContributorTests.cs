// =============================================================================
// Tests - ScalarCspContributor + UseGranitApiDocumentation wiring
// =============================================================================
// Vérifie que UseGranitApiDocumentation attache ScalarApiReferenceMetadata
// sur l'endpoint Scalar et enregistre ScalarCspContributor dans le registre
// CSP. Vérifie la composition CSP relâchée pour /scalar et stricte ailleurs.
// =============================================================================

using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Internal;
using Granit.Http.SecurityHeaders;
using Granit.Http.SecurityHeaders.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ScalarCspContributorTests
{
    [Fact]
    public void UseGranitApiDocumentation_Development_RegistersScalarCspContributor()
    {
        WebApplication app = BuildApp(Environments.Development, withSecurityHeaders: true);
        app.UseGranitApiDocumentation();

        ICspContributorRegistry registry = app.Services.GetRequiredService<ICspContributorRegistry>();

        registry.Contributors.Count.ShouldBe(1);
        registry.Contributors.Single().ShouldBeOfType<ScalarCspContributor>();
    }

    [Fact]
    public void UseGranitApiDocumentation_ProductionDisabled_DoesNotRegisterContributor()
    {
        WebApplication app = BuildApp(
            Environments.Production,
            withSecurityHeaders: true,
            enableScalarInProduction: false);
        app.UseGranitApiDocumentation();

        ICspContributorRegistry registry = app.Services.GetRequiredService<ICspContributorRegistry>();

        registry.Contributors.ShouldBeEmpty();
    }

    [Fact]
    public void UseGranitApiDocumentation_WithoutSecurityHeadersPackage_NoOps()
    {
        // ICspContributorRegistry is NOT registered → UseGranitApiDocumentation
        // logs Debug and skips the contributor registration without throwing.
        WebApplication app = BuildApp(Environments.Development, withSecurityHeaders: false);

        Should.NotThrow(() => app.UseGranitApiDocumentation());
        app.Services.GetService<ICspContributorRegistry>().ShouldBeNull();
    }

    [Fact]
    public async Task UseGranitApiDocumentation_AttachesScalarApiReferenceMetadata_OnScalarEndpoint()
    {
        // EndpointDataSource only materialises endpoints after the host has
        // started (or after a request goes through routing). We force
        // materialisation by starting + stopping the WebApplication; that
        // binds to an ephemeral local port and tears down cleanly.
        WebApplication app = BuildApp(Environments.Development, withSecurityHeaders: true);
        app.UseGranitApiDocumentation();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            EndpointDataSource dataSource = app.Services.GetRequiredService<EndpointDataSource>();
            IReadOnlyList<Endpoint> withMarker = [.. dataSource.Endpoints
                .Where(e => e.Metadata.GetMetadata<ScalarApiReferenceMetadata>() is not null)];

            withMarker.ShouldNotBeEmpty(
                "UseGranitApiDocumentation must attach ScalarApiReferenceMetadata on the Scalar route. " +
                $"Available endpoints: {string.Join(", ", dataSource.Endpoints.Select(e => e.DisplayName ?? "?"))}");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void ScalarCspContributor_OnScalarMarkedEndpoint_RelaxesCsp()
    {
        ScalarCspContributor contributor = new();
        CspBuilder builder = new();

        DefaultHttpContext ctx = new();
        ctx.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ScalarApiReferenceMetadata()),
            "scalar"));

        contributor.Contribute(ctx, builder);

        builder.Directives.ShouldContainKey("script-src");
        builder.Directives["script-src"].ShouldContain("'self'");
        builder.Directives["script-src"].ShouldContain("'unsafe-inline'");
        builder.Directives.ShouldContainKey("style-src");
        builder.Directives["style-src"].ShouldContain("'unsafe-inline'");
        builder.Directives.ShouldContainKey("font-src");
        builder.Directives["font-src"].ShouldContain("https://fonts.scalar.com");
        builder.Directives["font-src"].ShouldContain("data:");
        builder.Directives.ShouldContainKey("img-src");
        builder.Directives.ShouldContainKey("connect-src");
        builder.Directives["connect-src"].ShouldContain("'self'");
        builder.Directives["connect-src"].ShouldContain("https://api.scalar.com");
    }

    [Fact]
    public void ScalarCspContributor_OnNonScalarEndpoint_DoesNothing()
    {
        ScalarCspContributor contributor = new();
        CspBuilder builder = new();

        DefaultHttpContext ctx = new();
        // Endpoint without the marker
        ctx.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty,
            "api"));

        contributor.Contribute(ctx, builder);

        builder.Directives.ShouldBeEmpty();
    }

    [Fact]
    public void ScalarCspContributor_WithoutMatchedEndpoint_DoesNothing()
    {
        ScalarCspContributor contributor = new();
        CspBuilder builder = new();
        DefaultHttpContext ctx = new();   // No endpoint set

        contributor.Contribute(ctx, builder);

        builder.Directives.ShouldBeEmpty();
    }

    private static WebApplication BuildApp(
        string environmentName,
        bool withSecurityHeaders,
        bool enableScalarInProduction = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:Title"] = "Test API",
            ["ApiDocumentation:MajorVersions:0"] = "1",
            ["ApiDocumentation:EnableInProduction"] = enableScalarInProduction.ToString(),
        });

        if (withSecurityHeaders)
        {
            builder.AddGranitHttpSecurity();
        }

        builder.AddGranitApiDocumentation();
        return builder.Build();
    }
}

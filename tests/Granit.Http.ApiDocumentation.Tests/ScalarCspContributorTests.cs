// =============================================================================
// Tests - ScalarCspContributor + UseGranitApiDocumentation wiring
// =============================================================================
// Vérifie que UseGranitApiDocumentation attache ScalarApiReferenceMetadata
// sur l'endpoint Scalar et enregistre ScalarCspContributor dans le registre
// CSP. Vérifie la composition CSP relâchée pour /scalar et stricte ailleurs.
// =============================================================================

using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Internal;
using Granit.Http.ApiDocumentation.Options;
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
using MEOptions = Microsoft.Extensions.Options.Options;

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
        ScalarCspContributor contributor = BuildContributor();
        CspBuilder builder = new();

        contributor.Contribute(BuildScalarContext(), builder);

        builder.Directives.ShouldContainKey("script-src");
        builder.Directives["script-src"].ShouldContain("'self'");
        builder.Directives["script-src"].ShouldContain("'unsafe-inline'");
        builder.Directives["script-src"].ShouldContain("'unsafe-eval'");
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
    public void ScalarCspContributor_WithoutOAuth2_OnlyExposesScalarRegistryOrigin()
    {
        // OAuth2 left at defaults (both URLs null) → AddOrigin skipped silently
        ScalarCspContributor contributor = BuildContributor();
        CspBuilder builder = new();

        contributor.Contribute(BuildScalarContext(), builder);

        builder.Directives["connect-src"].ShouldBe(["'self'", "https://api.scalar.com"]);
    }

    [Fact]
    public void ScalarCspContributor_WithOAuth2KeycloakStyle_AddsSharedOriginOnce()
    {
        // Realistic Keycloak: auth and token endpoints share the same authority
        ScalarCspContributor contributor = BuildContributor(new OAuth2Options
        {
            AuthorizationUrl = "http://localhost:8080/realms/iot-showcase/protocol/openid-connect/auth",
            TokenUrl = "http://localhost:8080/realms/iot-showcase/protocol/openid-connect/token",
            ClientId = "iot-showcase-scalar",
        });
        CspBuilder builder = new();

        contributor.Contribute(BuildScalarContext(), builder);

        IReadOnlyCollection<string> connect = builder.Directives["connect-src"];
        connect.ShouldContain("http://localhost:8080");
        connect.Count(o => o == "http://localhost:8080").ShouldBe(1, "Distinct() must dedupe the shared IdP origin");
        connect.ShouldBe(["'self'", "https://api.scalar.com", "http://localhost:8080"]);
    }

    [Fact]
    public void ScalarCspContributor_WithOAuth2DistinctAuthAndTokenHosts_AddsBothOrigins()
    {
        // Split-host IdP setup (auth UI on 8080, token endpoint on 8081)
        ScalarCspContributor contributor = BuildContributor(new OAuth2Options
        {
            AuthorizationUrl = "http://localhost:8080/auth",
            TokenUrl = "http://localhost:8081/token",
            ClientId = "scalar",
        });
        CspBuilder builder = new();

        contributor.Contribute(BuildScalarContext(), builder);

        builder.Directives["connect-src"].ShouldContain("http://localhost:8080");
        builder.Directives["connect-src"].ShouldContain("http://localhost:8081");
    }

    [Theory]
    [InlineData("not a url")]                  // not absolute → TryCreate fails
    [InlineData("/relative/path/only")]        // absolute-path-only → TryCreate yields file:// on Linux; filtered by scheme guard
    [InlineData("ftp://idp.example.com")]      // valid URI but non-HTTP(S); filtered by scheme guard
    public void ScalarCspContributor_WithMalformedOrNonHttpOAuth2Urls_SilentlyIgnoresThem(string badUrl)
    {
        ScalarCspContributor contributor = BuildContributor(new OAuth2Options
        {
            AuthorizationUrl = badUrl,
            TokenUrl = badUrl,
            ClientId = "scalar",
        });
        CspBuilder builder = new();

        Should.NotThrow(() => contributor.Contribute(BuildScalarContext(), builder));

        builder.Directives["connect-src"].ShouldBe(["'self'", "https://api.scalar.com"]);
    }

    [Fact]
    public void ScalarCspContributor_OnNonScalarEndpoint_DoesNothing()
    {
        ScalarCspContributor contributor = BuildContributor();
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
        ScalarCspContributor contributor = BuildContributor();
        CspBuilder builder = new();
        DefaultHttpContext ctx = new();   // No endpoint set

        contributor.Contribute(ctx, builder);

        builder.Directives.ShouldBeEmpty();
    }

    private static ScalarCspContributor BuildContributor(OAuth2Options? oauth2 = null)
    {
        ApiDocumentationOptions options = new();
        if (oauth2 is not null)
        {
            options.OAuth2 = oauth2;
        }
        return new ScalarCspContributor(MEOptions.Create(options));
    }

    private static DefaultHttpContext BuildScalarContext()
    {
        DefaultHttpContext ctx = new();
        ctx.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ScalarApiReferenceMetadata()),
            "scalar"));
        return ctx;
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

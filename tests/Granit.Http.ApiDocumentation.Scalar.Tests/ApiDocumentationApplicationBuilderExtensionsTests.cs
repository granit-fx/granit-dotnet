// =============================================================================
// Tests - ApiDocumentationApplicationBuilderExtensions
// =============================================================================
// Vérifie que UseGranitApiDocumentation active les endpoints OpenAPI/Scalar
// en Development et en Production si Scalar:EnableInProduction est true, et
// est no-op sinon.
// =============================================================================

using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Scalar.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Scalar.Tests;

public sealed class ApiDocumentationApplicationBuilderExtensionsTests
{
    // --- Production + EnableInProduction=false → returns app without mapping routes ---

    [Fact]
    public void UseGranitApiDocumentation_ProductionDisabled_ReturnsAppWithoutMappingRoutes()
    {
        // Arrange
        WebApplication app = BuildApp(Environments.Production, new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:Scalar:EnableInProduction"] = "false",
        });

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    // --- Development → maps routes, returns app ---

    [Fact]
    public void UseGranitApiDocumentation_Development_MapsRoutesAndReturnsApp()
    {
        // Arrange
        WebApplication app = BuildApp(Environments.Development, new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Test API",
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
        });

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    // --- Production + EnableInProduction=true → maps routes, returns app ---

    [Fact]
    public void UseGranitApiDocumentation_ProductionEnabled_MapsRoutesAndReturnsApp()
    {
        // Arrange
        WebApplication app = BuildApp(Environments.Production, new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Prod API",
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "2",
            ["Http:ApiDocumentation:Scalar:EnableInProduction"] = "true",
        });

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    // --- AuthorizationPolicy = null → no explicit policy applied ---

    [Fact]
    public void UseGranitApiDocumentation_NullPolicy_MapsRoutesWithoutPolicy()
    {
        // Arrange
        WebApplication app = BuildApp(Environments.Development, new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
        });

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    // --- AuthorizationPolicy = "" → AllowAnonymous applied ---

    [Fact]
    public void UseGranitApiDocumentation_EmptyPolicy_MapsRoutesWithAllowAnonymous()
    {
        // Arrange
        WebApplication app = BuildApp(Environments.Development, new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:Scalar:AuthorizationPolicy"] = "",
        });

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    // --- AuthorizationPolicy = "InternalDeveloper" → RequireAuthorization applied ---

    [Fact]
    public void UseGranitApiDocumentation_NamedPolicy_MapsRoutesWithAuthorization()
    {
        // Arrange
        WebApplication app = BuildApp(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["Http:ApiDocumentation:MajorVersions:0"] = "1",
                ["Http:ApiDocumentation:Scalar:AuthorizationPolicy"] = "InternalDeveloper",
            },
            builder => builder.Services.AddAuthorizationBuilder()
                .AddPolicy("InternalDeveloper", p => p.RequireAuthenticatedUser()));

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }

    private static WebApplication BuildApp(
        string environmentName,
        Dictionary<string, string?> config,
        Action<WebApplicationBuilder>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.Configuration.AddInMemoryCollection(config);
        configure?.Invoke(builder);
        builder.AddGranitApiDocumentation();
        builder.Services.AddGranitApiDocumentationScalar();
        return builder.Build();
    }
}

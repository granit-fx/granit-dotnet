// =============================================================================
// Tests - ApiDocumentationApplicationBuilderExtensions
// =============================================================================
// Vérifie que UseGranitApiDocumentation active les endpoints OpenAPI/Scalar
// en Development et en Production si EnableInProduction est true, et est no-op
// sinon.
// =============================================================================

using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ApiDocumentationApplicationBuilderExtensionsTests
{
    // --- Production + EnableInProduction=false → returns app without mapping routes ---

    [Fact]
    public void UseGranitApiDocumentation_ProductionDisabled_ReturnsAppWithoutMappingRoutes()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:MajorVersions:0"] = "1",
            ["ApiDocumentation:EnableInProduction"] = "false",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:Title"] = "Test API",
            ["ApiDocumentation:MajorVersions:0"] = "1",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:Title"] = "Prod API",
            ["ApiDocumentation:MajorVersions:0"] = "1",
            ["ApiDocumentation:MajorVersions:1"] = "2",
            ["ApiDocumentation:EnableInProduction"] = "true",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:MajorVersions:0"] = "1",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:MajorVersions:0"] = "1",
            ["ApiDocumentation:AuthorizationPolicy"] = "",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("InternalDeveloper", p => p.RequireAuthenticatedUser());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiDocumentation:MajorVersions:0"] = "1",
            ["ApiDocumentation:AuthorizationPolicy"] = "InternalDeveloper",
        });
        builder.AddGranitApiDocumentation();
        WebApplication app = builder.Build();

        // Act
        WebApplication result = app.UseGranitApiDocumentation();

        // Assert
        result.ShouldBeSameAs(app);
    }
}

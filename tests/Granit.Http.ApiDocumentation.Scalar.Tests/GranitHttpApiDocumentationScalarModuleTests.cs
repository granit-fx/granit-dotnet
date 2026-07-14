// =============================================================================
// Tests - GranitHttpApiDocumentationScalarModule
// =============================================================================
// Vérifie que ConfigureServices lie ScalarOptions depuis la section
// Http:ApiDocumentation:Scalar, et que le module dépend du module
// ApiDocumentation (génération).
// =============================================================================

using Granit.Http.ApiDocumentation.Scalar.Options;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Scalar.Tests;

public sealed class GranitHttpApiDocumentationScalarModuleTests
{
    [Fact]
    public void ConfigureServices_BindsScalarOptions()
    {
        // Arrange
        GranitHttpApiDocumentationScalarModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Scalar:FaviconUrl"] = "/favicon.svg",
            ["Http:ApiDocumentation:Scalar:OAuth2:ClientId"] = "scalar-client",
        });
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ScalarOptions options = sp.GetRequiredService<IOptions<ScalarOptions>>().Value;
        options.FaviconUrl.ShouldBe("/favicon.svg");
        options.OAuth2.ClientId.ShouldBe("scalar-client");
    }

    [Fact]
    public void Module_DependsOnApiDocumentationModule()
    {
        var dependsOn = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitHttpApiDocumentationScalarModule), typeof(DependsOnAttribute));

        dependsOn.ShouldNotBeNull();
        dependsOn!.DependedTypes.ShouldContain(typeof(GranitHttpApiDocumentationModule));
    }
}

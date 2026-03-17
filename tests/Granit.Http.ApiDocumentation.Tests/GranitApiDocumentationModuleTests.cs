// =============================================================================
// Tests - GranitHttpApiDocumentationModule
// =============================================================================
// Vérifie que ConfigureServices enregistre les services de documentation OpenAPI.
// =============================================================================

using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class GranitHttpApiDocumentationModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersApiDocumentationOptions()
    {
        // Arrange
        GranitHttpApiDocumentationModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert — ApiDocumentationOptions should be resolvable
        using Microsoft.Extensions.DependencyInjection.ServiceProvider sp =
            builder.Services.BuildServiceProvider();
        ApiDocumentationOptions options =
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
        options.ShouldNotBeNull();
        options.MajorVersions.ShouldHaveSingleItem().ShouldBe(1, "default version is 1");
    }
}

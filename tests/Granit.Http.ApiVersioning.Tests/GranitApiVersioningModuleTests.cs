// =============================================================================
// Tests - GranitHttpApiVersioningModule
// =============================================================================
// Vérifie que ConfigureServices déclenche l'enregistrement des services
// de versioning via AddGranitApiVersioning.
// =============================================================================

using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiVersioning.Tests;

public sealed class GranitHttpApiVersioningModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersApiVersioningServices()
    {
        // Arrange
        GranitHttpApiVersioningModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert — GranitApiVersioningOptions should be resolvable after services are built
        using Microsoft.Extensions.DependencyInjection.ServiceProvider sp =
            builder.Services.BuildServiceProvider();
        Options.GranitApiVersioningOptions options =
            sp.GetRequiredService<IOptions<Options.GranitApiVersioningOptions>>().Value;
        options.ShouldNotBeNull();
        options.DefaultMajorVersion.ShouldBe(1);
    }
}

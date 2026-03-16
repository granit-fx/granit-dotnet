// =============================================================================
// Tests - GranitGuidsModule
// =============================================================================
// Verifies that the module registers Guids services via ConfigureServices.
// =============================================================================

using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class GranitGuidsModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersUuidV7GuidGeneratorByDefault()
    {
        // Arrange
        GranitGuidsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        IGuidGenerator? generator = sp.GetService<IGuidGenerator>();
        generator.ShouldNotBeNull();
        generator.ShouldBeOfType<UuidV7GuidGenerator>();
    }
}

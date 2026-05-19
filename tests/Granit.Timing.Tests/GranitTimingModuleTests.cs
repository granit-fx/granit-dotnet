// =============================================================================
// Tests - GranitTimingModule
// =============================================================================
// Vérifie que le module enregistre les services Timing via ConfigureServices.
// =============================================================================

using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class GranitTimingModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersTimingServices()
    {
        // Arrange
        GranitTimingModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        IClock? clock = sp.GetService<IClock>();
        clock.ShouldNotBeNull();

        ICurrentTimezoneProvider? tzProvider = sp.GetService<ICurrentTimezoneProvider>();
        tzProvider.ShouldNotBeNull();

        TimeProvider? timeProvider = sp.GetService<TimeProvider>();
        timeProvider.ShouldNotBeNull();
    }
}

// =============================================================================
// Tests - TimingServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitTiming enregistre les services attendus.
// =============================================================================

using Granit.Timing.Extensions;
using Granit.Timing.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class TimingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTiming_RegistersClock()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IClock? clock = sp.GetService<IClock>();
        clock.ShouldNotBeNull();
        clock.ShouldBeOfType<Clock>();
    }

    [Fact]
    public void AddGranitTiming_RegistersTimezoneProvider()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        ICurrentTimezoneProvider? provider = sp.GetService<ICurrentTimezoneProvider>();
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CurrentTimezoneProvider>();
    }

    [Fact]
    public void AddGranitTiming_RegistersFirstDayOfWeekProvider()
    {
        ServiceCollection services = new();

        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider();

        ICurrentFirstDayOfWeekProvider? provider = sp.GetService<ICurrentFirstDayOfWeekProvider>();
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CurrentFirstDayOfWeekProvider>();
    }

    [Fact]
    public void AddGranitTiming_RegistersPeriodResolver()
    {
        ServiceCollection services = new();

        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = sp.CreateScope();

        IPeriodResolver? resolver = scope.ServiceProvider.GetService<IPeriodResolver>();
        resolver.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTiming_RegistersTimeProvider()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        TimeProvider? timeProvider = sp.GetService<TimeProvider>();
        timeProvider.ShouldNotBeNull();
        timeProvider.ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddGranitTiming_TryAddSingleton_DoesNotOverrideExisting()
    {
        // Arrange
        ServiceCollection services = new();
        IClock customClock = NSubstitute.Substitute.For<IClock>();
        services.AddSingleton(customClock);

        // Act
        services.AddGranitTiming();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IClock resolved = sp.GetRequiredService<IClock>();
        resolved.ShouldBeSameAs(customClock);
    }

    [Fact]
    public void AddGranitTiming_WithConfigure_AppliesOptions()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming(opts =>
        {
            opts.DefaultTimezone = "America/New_York";
        });

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        ClockOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClockOptions>>().Value;
        options.DefaultTimezone.ShouldBe("America/New_York");
    }

    [Fact]
    public void AddGranitTiming_WithoutConfigure_DoesNotRegisterOptions()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming();

        // Assert - no IConfigureOptions<ClockOptions> registered
        ServiceDescriptor? configureDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<ClockOptions>));
        configureDescriptor.ShouldBeNull();
    }

    [Fact]
    public void AddGranitTiming_CalledTwice_DoesNotDuplicateRegistrations()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitTiming();
        services.AddGranitTiming();

        // Assert - TryAddSingleton should prevent duplicates
        int clockCount = services.Count(d => d.ServiceType == typeof(IClock));
        clockCount.ShouldBe(1);

        int tzProviderCount = services.Count(d => d.ServiceType == typeof(ICurrentTimezoneProvider));
        tzProviderCount.ShouldBe(1);

        int timeProviderCount = services.Count(d => d.ServiceType == typeof(TimeProvider));
        timeProviderCount.ShouldBe(1);
    }

    [Fact]
    public void AddGranitTiming_ReturnsServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitTiming();

        // Assert - fluent API returns same instance
        result.ShouldBeSameAs(services);
    }
}

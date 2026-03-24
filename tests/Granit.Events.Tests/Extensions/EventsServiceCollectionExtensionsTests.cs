using Granit.Events;
using Granit.Events.Diagnostics;
using Granit.Events.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests.Extensions;

public sealed class EventsServiceCollectionExtensionsTests : IDisposable
{
    private readonly ServiceProvider _sp;

    public EventsServiceCollectionExtensionsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddGranitEvents();
        _sp = services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void AddGranitEvents_RegistersEventsMetrics_AsSingleton()
    {
        EventsMetrics metrics1 = _sp.GetRequiredService<EventsMetrics>();
        EventsMetrics metrics2 = _sp.GetRequiredService<EventsMetrics>();

        metrics1.ShouldBeSameAs(metrics2);
    }

    [Fact]
    public void AddGranitEvents_RegistersLocalEventBus_AsScoped()
    {
        using IServiceScope scope = _sp.CreateScope();
        ILocalEventBus bus = scope.ServiceProvider.GetRequiredService<ILocalEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitEvents_RegistersDistributedEventBus_AsScoped()
    {
        using IServiceScope scope = _sp.CreateScope();
        IDistributedEventBus bus = scope.ServiceProvider.GetRequiredService<IDistributedEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitEvents_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();
        services.AddMetrics();

        IServiceCollection result = services.AddGranitEvents();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitEvents_CalledTwice_DoesNotDuplicateRegistrations()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddGranitEvents();
        services.AddGranitEvents();

        int metricsCount = services.Count(d =>
            d.ServiceType == typeof(EventsMetrics));
        int localBusCount = services.Count(d =>
            d.ServiceType == typeof(ILocalEventBus));
        int distributedBusCount = services.Count(d =>
            d.ServiceType == typeof(IDistributedEventBus));

        metricsCount.ShouldBe(1);
        localBusCount.ShouldBe(1);
        distributedBusCount.ShouldBe(1);
    }
}

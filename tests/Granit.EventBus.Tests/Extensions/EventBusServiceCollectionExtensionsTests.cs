using Granit.Core.Events;
using Granit.EventBus.Diagnostics;
using Granit.EventBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests.Extensions;

public sealed class EventBusServiceCollectionExtensionsTests : IDisposable
{
    private readonly ServiceProvider _sp;

    public EventBusServiceCollectionExtensionsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddGranitEventBus();
        _sp = services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void AddGranitEventBus_RegistersEventBusMetrics_AsSingleton()
    {
        EventBusMetrics metrics1 = _sp.GetRequiredService<EventBusMetrics>();
        EventBusMetrics metrics2 = _sp.GetRequiredService<EventBusMetrics>();

        metrics1.ShouldBeSameAs(metrics2);
    }

    [Fact]
    public void AddGranitEventBus_RegistersLocalEventBus_AsScoped()
    {
        using IServiceScope scope = _sp.CreateScope();
        ILocalEventBus bus = scope.ServiceProvider.GetRequiredService<ILocalEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitEventBus_RegistersDistributedEventBus_AsScoped()
    {
        using IServiceScope scope = _sp.CreateScope();
        IDistributedEventBus bus = scope.ServiceProvider.GetRequiredService<IDistributedEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitEventBus_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();
        services.AddMetrics();

        IServiceCollection result = services.AddGranitEventBus();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitEventBus_CalledTwice_DoesNotDuplicateRegistrations()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddGranitEventBus();
        services.AddGranitEventBus();

        int metricsCount = services.Count(d =>
            d.ServiceType == typeof(EventBusMetrics));
        int localBusCount = services.Count(d =>
            d.ServiceType == typeof(ILocalEventBus));
        int distributedBusCount = services.Count(d =>
            d.ServiceType == typeof(IDistributedEventBus));

        metricsCount.ShouldBe(1);
        localBusCount.ShouldBe(1);
        distributedBusCount.ShouldBe(1);
    }
}

using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Events.Diagnostics;
using Granit.Events.Wolverine.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class WolverineIntegrationEventDispatcherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private static WolverineHostReadiness CreateReadiness(bool isReady)
    {
        WolverineHostReadiness readiness = new(NullLogger<WolverineHostReadiness>.Instance);
        if (isReady)
        {
            ((IHostedLifecycleService)readiness).StartedAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        return readiness;
    }

    private static EventsMetrics CreateMetrics()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>())
            .Returns(ci => new Meter(ci.Arg<MeterOptions>().Name));
        return new EventsMetrics(meterFactory);
    }

    [Fact]
    public async Task DispatchAsync_WhenReady_PublishesEachEventViaMessageBus()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineIntegrationEventDispatcher>.Instance);
        TestIntegrationEvent evt1 = new();
        TestIntegrationEvent evt2 = new();

        await sut.DispatchAsync([evt1, evt2], TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<IIntegrationEvent>(e => ReferenceEquals(e, evt1)));
        await _bus.Received(1).PublishAsync(Arg.Is<IIntegrationEvent>(e => ReferenceEquals(e, evt2)));
    }

    [Fact]
    public async Task DispatchAsync_WhenReady_EmptyList_DoesNotCallPublish()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineIntegrationEventDispatcher>.Instance);

        await sut.DispatchAsync([], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IIntegrationEvent>());
    }

    [Fact]
    public async Task DispatchAsync_WhenReady_PublishesExpectedCount()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineIntegrationEventDispatcher>.Instance);
        IReadOnlyList<IIntegrationEvent> events = [new TestIntegrationEvent(), new TestIntegrationEvent(), new TestIntegrationEvent()];

        await sut.DispatchAsync(events, TestContext.Current.CancellationToken);

        await _bus.Received(3).PublishAsync(Arg.Any<IIntegrationEvent>());
    }

    [Fact]
    public async Task DispatchAsync_WhenNotReady_SkipsWithoutCallingMessageBus()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus, CreateReadiness(false), CreateMetrics(),
            NullLogger<WolverineIntegrationEventDispatcher>.Instance);
        TestIntegrationEvent evt = new();

        await sut.DispatchAsync([evt], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IIntegrationEvent>());
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;
}

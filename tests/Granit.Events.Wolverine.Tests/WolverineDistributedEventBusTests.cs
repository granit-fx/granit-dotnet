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

public sealed class WolverineDistributedEventBusTests
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
    public async Task PublishAsync_WhenReady_DelegatesToMessageBus()
    {
        WolverineDistributedEventBus sut = new(_bus, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineDistributedEventBus>.Instance);
        TestIntegrationEvent evt = new();

        await sut.PublishAsync(evt, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(evt);
    }

    [Fact]
    public async Task PublishAsync_WhenNotReady_SkipsWithoutCallingMessageBus()
    {
        WolverineDistributedEventBus sut = new(_bus, CreateReadiness(false), CreateMetrics(),
            NullLogger<WolverineDistributedEventBus>.Instance);
        TestIntegrationEvent evt = new();

        await sut.PublishAsync(evt, TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<TestIntegrationEvent>());
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        WolverineDistributedEventBus sut = new(_bus, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineDistributedEventBus>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.PublishAsync<TestIntegrationEvent>(null!, TestContext.Current.CancellationToken));
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;
}

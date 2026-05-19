using System.Diagnostics.Metrics;
using Granit.Events.Diagnostics;
using Granit.Events.Wolverine.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class WolverineDomainEventDispatcherTests
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
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineDomainEventDispatcher sut = new(_bus, sp, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineDomainEventDispatcher>.Instance);
        TestDomainEvent evt1 = new();
        TestDomainEvent evt2 = new();

        await sut.DispatchAsync([evt1, evt2], TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<IDomainEvent>(e => ReferenceEquals(e, evt1)));
        await _bus.Received(1).PublishAsync(Arg.Is<IDomainEvent>(e => ReferenceEquals(e, evt2)));
    }

    [Fact]
    public async Task DispatchAsync_WhenReady_EmptyList_DoesNotCallPublish()
    {
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineDomainEventDispatcher sut = new(_bus, sp, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineDomainEventDispatcher>.Instance);

        await sut.DispatchAsync([], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task DispatchAsync_WhenReady_PublishesExpectedCount()
    {
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineDomainEventDispatcher sut = new(_bus, sp, CreateReadiness(true), CreateMetrics(),
            NullLogger<WolverineDomainEventDispatcher>.Instance);
        IReadOnlyList<IDomainEvent> events = [new TestDomainEvent(), new TestDomainEvent(), new TestDomainEvent()];

        await sut.DispatchAsync(events, TestContext.Current.CancellationToken);

        await _bus.Received(3).PublishAsync(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task DispatchAsync_WhenNotReady_FallsBackToLocalHandlers()
    {
        TestDomainEventHandler handler = new();
        ServiceCollection services = [];
        services.AddSingleton<ILocalEventHandler<TestDomainEvent>>(handler);
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineDomainEventDispatcher sut = new(_bus, sp, CreateReadiness(false), CreateMetrics(),
            NullLogger<WolverineDomainEventDispatcher>.Instance);
        TestDomainEvent evt = new();

        await sut.DispatchAsync([evt], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>());
        handler.Received.ShouldContain(evt);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotReady_NoHandlers_CompletesSuccessfully()
    {
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineDomainEventDispatcher sut = new(_bus, sp, CreateReadiness(false), CreateMetrics(),
            NullLogger<WolverineDomainEventDispatcher>.Instance);

        await Should.NotThrowAsync(
            () => sut.DispatchAsync([new TestDomainEvent()], TestContext.Current.CancellationToken));
    }

    private sealed record TestDomainEvent : IDomainEvent;

    private sealed class TestDomainEventHandler : ILocalEventHandler<TestDomainEvent>
    {
        public List<TestDomainEvent> Received { get; } = [];

        public Task HandleAsync(TestDomainEvent localEvent, CancellationToken cancellationToken = default)
        {
            Received.Add(localEvent);
            return Task.CompletedTask;
        }
    }
}

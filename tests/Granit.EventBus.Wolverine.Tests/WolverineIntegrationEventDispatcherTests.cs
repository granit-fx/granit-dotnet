using Granit.Core.Events;
using Granit.EventBus.Wolverine.Internal;
using NSubstitute;
using Wolverine;
using Xunit;

namespace Granit.EventBus.Wolverine.Tests;

public sealed class WolverineIntegrationEventDispatcherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task DispatchAsync_PublishesEachEventViaMessageBus()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus);
        TestIntegrationEvent evt1 = new();
        TestIntegrationEvent evt2 = new();

        await sut.DispatchAsync([evt1, evt2], TestContext.Current.CancellationToken);

        // IIntegrationEvent generic parameter matches the implementation's T inference
        await _bus.Received(1).PublishAsync(Arg.Is<IIntegrationEvent>(e => ReferenceEquals(e, evt1)));
        await _bus.Received(1).PublishAsync(Arg.Is<IIntegrationEvent>(e => ReferenceEquals(e, evt2)));
    }

    [Fact]
    public async Task DispatchAsync_EmptyList_DoesNotCallPublish()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus);

        await sut.DispatchAsync([], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IIntegrationEvent>());
    }

    [Fact]
    public async Task DispatchAsync_PublishesExpectedCount()
    {
        WolverineIntegrationEventDispatcher sut = new(_bus);
        IReadOnlyList<IIntegrationEvent> events = [new TestIntegrationEvent(), new TestIntegrationEvent(), new TestIntegrationEvent()];

        await sut.DispatchAsync(events, TestContext.Current.CancellationToken);

        await _bus.Received(3).PublishAsync(Arg.Any<IIntegrationEvent>());
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;
}

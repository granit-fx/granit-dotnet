using Granit.Events;
using Granit.Events.Wolverine.Internal;
using NSubstitute;
using Wolverine;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class WolverineDomainEventDispatcherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task DispatchAsync_PublishesEachEventViaMessageBus()
    {
        WolverineDomainEventDispatcher sut = new(_bus);
        TestDomainEvent evt1 = new();
        TestDomainEvent evt2 = new();

        await sut.DispatchAsync([evt1, evt2], TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<IDomainEvent>(e => ReferenceEquals(e, evt1)));
        await _bus.Received(1).PublishAsync(Arg.Is<IDomainEvent>(e => ReferenceEquals(e, evt2)));
    }

    [Fact]
    public async Task DispatchAsync_EmptyList_DoesNotCallPublish()
    {
        WolverineDomainEventDispatcher sut = new(_bus);

        await sut.DispatchAsync([], TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task DispatchAsync_PublishesExpectedCount()
    {
        WolverineDomainEventDispatcher sut = new(_bus);
        IReadOnlyList<IDomainEvent> events = [new TestDomainEvent(), new TestDomainEvent(), new TestDomainEvent()];

        await sut.DispatchAsync(events, TestContext.Current.CancellationToken);

        await _bus.Received(3).PublishAsync(Arg.Any<IDomainEvent>());
    }

    private sealed record TestDomainEvent : IDomainEvent;
}

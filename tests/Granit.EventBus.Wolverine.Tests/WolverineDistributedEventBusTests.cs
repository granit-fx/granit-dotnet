using Granit.Core.Events;
using Granit.EventBus.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.EventBus.Wolverine.Tests;

public sealed class WolverineDistributedEventBusTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task PublishAsync_DelegatesToMessageBus()
    {
        WolverineDistributedEventBus sut = new(_bus);
        TestIntegrationEvent evt = new();

        await sut.PublishAsync(evt, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(evt);
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        WolverineDistributedEventBus sut = new(_bus);

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.PublishAsync<TestIntegrationEvent>(null!, TestContext.Current.CancellationToken));
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;
}

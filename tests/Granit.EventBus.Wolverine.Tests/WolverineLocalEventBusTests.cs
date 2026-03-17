using Granit.Core.Events;
using Granit.EventBus.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.EventBus.Wolverine.Tests;

public sealed class WolverineLocalEventBusTests
{
    private sealed record TestEvent(string Value);

    [Fact]
    public async Task PublishAsync_DelegatesToMessageBus()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineLocalEventBus localBus = new(bus);
        TestEvent evt = new("test");

        await localBus.PublishAsync(evt, TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(evt);
    }
}

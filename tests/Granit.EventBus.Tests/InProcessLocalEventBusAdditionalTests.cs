using Granit.Core.Events;
using Granit.EventBus.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests;

public sealed class InProcessLocalEventBusAdditionalTests
{
    private sealed record TestEvent(string Value);

    private sealed class TestHandler : ILocalEventHandler<TestEvent>
    {
        public TestEvent? Received { get; private set; }

        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
        {
            Received = localEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : ILocalEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }

    private sealed class CancellingHandler : ILocalEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException("cancelled");
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            bus.PublishAsync<TestEvent>(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_ContinuesWithNextHandler()
    {
        TestHandler survivingHandler = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new ThrowingHandler());
        services.AddSingleton<ILocalEventHandler<TestEvent>>(survivingHandler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance);

        await bus.PublishAsync(new TestEvent("resilient"), TestContext.Current.CancellationToken);

        survivingHandler.Received.ShouldNotBeNull();
        survivingHandler.Received!.Value.ShouldBe("resilient");
    }

    [Fact]
    public async Task PublishAsync_OperationCanceledException_PropagatesException()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new CancellingHandler());
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            bus.PublishAsync(new TestEvent("cancel-me"), TestContext.Current.CancellationToken));
    }
}

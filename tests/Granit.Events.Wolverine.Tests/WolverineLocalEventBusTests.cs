using Granit.Events;
using Granit.Events.Wolverine.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class WolverineLocalEventBusTests
{
    private sealed record TestEvent(string Value);

    private sealed class TestEventHandler : ILocalEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = [];

        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
        {
            Received.Add(localEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTestEventHandler : ILocalEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Handler failed");
    }

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

    [Fact]
    public async Task PublishAsync_WhenReady_DelegatesToMessageBus()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineLocalEventBus sut = new(bus, sp, CreateReadiness(true),
            NullLogger<WolverineLocalEventBus>.Instance);
        TestEvent evt = new("test");

        await sut.PublishAsync(evt, TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(evt);
    }

    [Fact]
    public async Task PublishAsync_WhenNotReady_FallsBackToDirectHandlers()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        TestEventHandler handler = new();
        ServiceCollection services = [];
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler);
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineLocalEventBus sut = new(bus, sp, CreateReadiness(false),
            NullLogger<WolverineLocalEventBus>.Instance);
        TestEvent evt = new("test");

        await sut.PublishAsync(evt, TestContext.Current.CancellationToken);

        await bus.DidNotReceive().PublishAsync(Arg.Any<TestEvent>());
        handler.Received.ShouldContain(evt);
    }

    [Fact]
    public async Task PublishAsync_WhenNotReady_HandlerThrows_DoesNotPropagate()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        ServiceCollection services = [];
        services.AddSingleton<ILocalEventHandler<TestEvent>, FailingTestEventHandler>();
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineLocalEventBus sut = new(bus, sp, CreateReadiness(false),
            NullLogger<WolverineLocalEventBus>.Instance);
        TestEvent evt = new("test");

        await Should.NotThrowAsync(
            () => sut.PublishAsync(evt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        ServiceCollection services = [];
        using ServiceProvider sp = services.BuildServiceProvider();
        WolverineLocalEventBus sut = new(bus, sp, CreateReadiness(true),
            NullLogger<WolverineLocalEventBus>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.PublishAsync<TestEvent>(null!, TestContext.Current.CancellationToken));
    }
}

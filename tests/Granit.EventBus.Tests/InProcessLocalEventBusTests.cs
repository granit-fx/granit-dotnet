using System.Diagnostics.Metrics;
using Granit.Core.Events;
using Granit.EventBus.Diagnostics;
using Granit.EventBus.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests;

public sealed class InProcessLocalEventBusTests : IDisposable
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

    private readonly ServiceProvider _metricsSp;
    private readonly EventBusMetrics _metrics;

    public InProcessLocalEventBusTests()
    {
        ServiceCollection metricsServices = new();
        metricsServices.AddMetrics();
        _metricsSp = metricsServices.BuildServiceProvider();
        _metrics = new EventBusMetrics(_metricsSp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _metricsSp.Dispose();

    [Fact]
    public async Task PublishAsync_CallsRegisteredHandler()
    {
        TestHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestEvent("hello"), TestContext.Current.CancellationToken);

        handler.Received.ShouldNotBeNull();
        handler.Received!.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_DoesNotThrow()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        await Should.NotThrowAsync(() =>
            bus.PublishAsync(new TestEvent("lonely"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_AllCalled()
    {
        TestHandler handler1 = new();
        TestHandler handler2 = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler1);
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler2);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestEvent("both"), TestContext.Current.CancellationToken);

        handler1.Received.ShouldNotBeNull();
        handler2.Received.ShouldNotBeNull();
    }
}

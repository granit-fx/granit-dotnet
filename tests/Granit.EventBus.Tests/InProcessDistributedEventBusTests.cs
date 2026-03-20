using System.Diagnostics.Metrics;
using Granit.Core.Events;
using Granit.EventBus.Diagnostics;
using Granit.EventBus.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests;

public sealed class InProcessDistributedEventBusTests : IDisposable
{
    private sealed record TestIntegrationEvent(string Value) : IIntegrationEvent;

    private sealed class TestHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public TestIntegrationEvent? Received { get; private set; }

        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Received = integrationEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }

    private readonly ServiceProvider _metricsSp;
    private readonly EventBusMetrics _metrics;

    public InProcessDistributedEventBusTests()
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
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("hello"), TestContext.Current.CancellationToken);

        handler.Received.ShouldNotBeNull();
        handler.Received!.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_DoesNotThrow()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await Should.NotThrowAsync(() =>
            bus.PublishAsync(new TestIntegrationEvent("lonely"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_AllCalled()
    {
        TestHandler handler1 = new();
        TestHandler handler2 = new();
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler1);
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler2);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("both"), TestContext.Current.CancellationToken);

        handler1.Received.ShouldNotBeNull();
        handler2.Received.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_ContinuesWithNext()
    {
        TestHandler survivingHandler = new();
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(new ThrowingHandler());
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(survivingHandler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("resilient"), TestContext.Current.CancellationToken);

        survivingHandler.Received.ShouldNotBeNull();
        survivingHandler.Received!.Value.ShouldBe("resilient");
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            bus.PublishAsync<TestIntegrationEvent>(null!, TestContext.Current.CancellationToken));
    }
}

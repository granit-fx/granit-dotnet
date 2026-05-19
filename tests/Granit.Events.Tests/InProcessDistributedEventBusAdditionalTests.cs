using System.Diagnostics.Metrics;
using Granit.Events.Diagnostics;
using Granit.Events.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests;

public sealed class InProcessDistributedEventBusAdditionalTests : IDisposable
{
    private sealed record TestIntegrationEvent(string Value) : IIntegrationEvent;

    private sealed class CancellingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException("cancelled");
    }

    private sealed class TrackingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public int CallCount { get; private set; }

        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private readonly ServiceProvider _metricsSp;
    private readonly IMeterFactory _meterFactory;
    private readonly EventsMetrics _metrics;

    public InProcessDistributedEventBusAdditionalTests()
    {
        ServiceCollection metricsServices = new();
        metricsServices.AddMetrics();
        _metricsSp = metricsServices.BuildServiceProvider();
        _meterFactory = _metricsSp.GetRequiredService<IMeterFactory>();
        _metrics = new EventsMetrics(_meterFactory);
    }

    public void Dispose() => _metricsSp.Dispose();

    [Fact]
    public async Task PublishAsync_OperationCanceledException_PropagatesException()
    {
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(new CancellingHandler());
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            bus.PublishAsync(new TestIntegrationEvent("cancel-me"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_CalledMultipleTimes_AllHandlersInvoked()
    {
        TrackingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("first"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new TestIntegrationEvent("second"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new TestIntegrationEvent("third"), TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task PublishAsync_RecordsMetrics_ForEachPublish()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, EventsMetrics.MeterName, "granit.events.event.published");

        TrackingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("one"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new TestIntegrationEvent("two"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(2);
        snapshot[0].Tags["bus_type"].ShouldBe("distributed");
        snapshot[0].Tags["event_type"].ShouldBe("TestIntegrationEvent");
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_RecordsErrorMetric()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, EventsMetrics.MeterName, "granit.events.handler.executed");

        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(
            new CancellingHandler()); // will throw but be swallowed... wait, OCE propagates

        // Use a non-OCE throwing handler instead
        TrackingHandler successHandler = new();
        ServiceCollection services2 = new();
        services2.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(
            new ThrowingHandler());
        services2.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(successHandler);
        ServiceProvider sp = services2.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        await bus.PublishAsync(new TestIntegrationEvent("test"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(2);

        CollectedMeasurement<long> errorMeasurement = snapshot.First(m => (string)m.Tags["status"]! == "error");
        errorMeasurement.Tags["event_type"].ShouldBe("TestIntegrationEvent");

        CollectedMeasurement<long> successMeasurement = snapshot.First(m => (string)m.Tags["status"]! == "success");
        successMeasurement.Tags["event_type"].ShouldBe("TestIntegrationEvent");
    }

    private sealed class ThrowingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }
}

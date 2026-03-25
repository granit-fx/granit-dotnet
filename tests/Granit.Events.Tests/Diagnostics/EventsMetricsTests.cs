using System.Diagnostics.Metrics;
using Granit.Events.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests.Diagnostics;

public sealed class EventsMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly EventsMetrics _metrics;

    public EventsMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new EventsMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordEventPublished_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EventsMetrics.MeterName, "granit.events.event.published");

        _metrics.RecordEventPublished("tenant-123", "local", "OrderCreatedEvent");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["bus_type"].ShouldBe("local");
        snapshot[0].Tags["event_type"].ShouldBe("OrderCreatedEvent");
    }

    [Fact]
    public void RecordEventPublished_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EventsMetrics.MeterName, "granit.events.event.published");

        _metrics.RecordEventPublished(null, "distributed", "TestEto");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordHandlerExecuted_Success_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EventsMetrics.MeterName, "granit.events.handler.executed");

        _metrics.RecordHandlerExecuted("tenant-1", "OrderCreatedEvent", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["event_type"].ShouldBe("OrderCreatedEvent");
        snapshot[0].Tags["status"].ShouldBe("success");
    }

    [Fact]
    public void RecordHandlerExecuted_Error_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EventsMetrics.MeterName, "granit.events.handler.executed");

        _metrics.RecordHandlerExecuted("tenant-1", "OrderCreatedEvent", "error");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["status"].ShouldBe("error");
    }

    [Fact]
    public void RecordHandlerExecuted_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EventsMetrics.MeterName, "granit.events.handler.executed");

        _metrics.RecordHandlerExecuted(null, "TestEvent", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

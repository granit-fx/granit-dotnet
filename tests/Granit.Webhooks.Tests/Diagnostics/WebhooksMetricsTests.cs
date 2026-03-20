using System.Diagnostics.Metrics;
using Granit.Webhooks.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Diagnostics;

public sealed class WebhooksMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly WebhooksMetrics _metrics;

    public WebhooksMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WebhooksMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    // -------------------------------------------------------------------------
    // RecordFanoutTriggered
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordFanoutTriggered_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.fanout.triggered");

        _metrics.RecordFanoutTriggered("tenant-123", "order.created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["event_type"].ShouldBe("order.created");
    }

    [Fact]
    public void RecordFanoutTriggered_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.fanout.triggered");

        _metrics.RecordFanoutTriggered(null, "order.created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // -------------------------------------------------------------------------
    // RecordDeliverySucceeded
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordDeliverySucceeded_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.deliveries.succeeded");

        _metrics.RecordDeliverySucceeded("tenant-1", "document.uploaded");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["event_type"].ShouldBe("document.uploaded");
    }

    // -------------------------------------------------------------------------
    // RecordDeliveryFailed
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordDeliveryFailed_IncrementsWithHttpStatus()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.deliveries.failed");

        _metrics.RecordDeliveryFailed("tenant-1", "order.created", 503);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["http_status"].ShouldBe("503");
    }

    [Fact]
    public void RecordDeliveryFailed_NullHttpStatus_UsesTimeout()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.deliveries.failed");

        _metrics.RecordDeliveryFailed("tenant-1", "order.created", httpStatus: null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["http_status"].ShouldBe("timeout");
    }

    // -------------------------------------------------------------------------
    // RecordSubscriptionSuspended
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordSubscriptionSuspended_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.subscriptions.suspended");

        _metrics.RecordSubscriptionSuspended("tenant-1", 401);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["http_status"].ShouldBe("401");
    }

    [Fact]
    public void RecordSubscriptionSuspended_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.subscriptions.suspended");

        _metrics.RecordSubscriptionSuspended(null, 404);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // -------------------------------------------------------------------------
    // RecordDeliveryDuration
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordDeliveryDuration_RecordsHistogramInSeconds()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.delivery.duration");

        _metrics.RecordDeliveryDuration("tenant-1", "order.created", "succeeded", TimeSpan.FromSeconds(1.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.5, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["event_type"].ShouldBe("order.created");
        snapshot[0].Tags["status"].ShouldBe("succeeded");
    }

    [Fact]
    public void RecordDeliveryDuration_FailedStatus_RecordsTags()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, WebhooksMetrics.MeterName, "granit.webhooks.delivery.duration");

        _metrics.RecordDeliveryDuration(null, "test.event", "failed", TimeSpan.FromMilliseconds(250));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(0.25, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
        snapshot[0].Tags["status"].ShouldBe("failed");
    }
}

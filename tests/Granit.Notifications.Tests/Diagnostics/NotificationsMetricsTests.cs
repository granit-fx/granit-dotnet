using System.Diagnostics.Metrics;
using Granit.Notifications.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Diagnostics;

public sealed class NotificationsMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly NotificationsMetrics _metrics;

    public NotificationsMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new NotificationsMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordFanoutTriggered_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.fanout.triggered");

        _metrics.RecordFanoutTriggered("tenant-123", "invoice.created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["notification_type"].ShouldBe("invoice.created");
    }

    [Fact]
    public void RecordFanoutTriggered_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.fanout.triggered");

        _metrics.RecordFanoutTriggered(null, "test.notification");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordDeliverySucceeded_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.deliveries.succeeded");

        _metrics.RecordDeliverySucceeded("tenant-1", "email", "invoice.created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["channel"].ShouldBe("email");
        snapshot[0].Tags["notification_type"].ShouldBe("invoice.created");
    }

    [Fact]
    public void RecordDeliverySucceeded_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.deliveries.succeeded");

        _metrics.RecordDeliverySucceeded(null, "in_app", "test.notification");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordDeliveryFailed_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.deliveries.failed");

        _metrics.RecordDeliveryFailed("tenant-1", "push", "alert.security");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["channel"].ShouldBe("push");
        snapshot[0].Tags["notification_type"].ShouldBe("alert.security");
    }

    [Fact]
    public void RecordDeliveryFailed_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.deliveries.failed");

        _metrics.RecordDeliveryFailed(null, "email", "test.notification");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordDeliveryDuration_RecordsHistogramWithCorrectTags()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.delivery.duration");

        _metrics.RecordDeliveryDuration("tenant-1", "email", "success", TimeSpan.FromSeconds(1.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.5, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["channel"].ShouldBe("email");
        snapshot[0].Tags["status"].ShouldBe("success");
    }

    [Fact]
    public void RecordDeliveryDuration_FailureStatus_RecordsCorrectly()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.delivery.duration");

        _metrics.RecordDeliveryDuration("tenant-1", "push", "failure", TimeSpan.FromSeconds(0.25));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(0.25, 0.01);
        snapshot[0].Tags["status"].ShouldBe("failure");
    }

    [Fact]
    public void RecordDeliveryDuration_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, NotificationsMetrics.MeterName, "granit.notifications.delivery.duration");

        _metrics.RecordDeliveryDuration(null, "in_app", "success", TimeSpan.FromSeconds(0.1));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

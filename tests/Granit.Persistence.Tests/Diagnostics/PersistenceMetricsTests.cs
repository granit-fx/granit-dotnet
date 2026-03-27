// =============================================================================
// Tests - PersistenceMetrics
// =============================================================================
// Verifie que chaque methode Record* incremente le bon compteur avec les tags
// attendus (tenant_id, operation, event_type) et que le fallback "global"
// s'applique quand le tenant est null.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Persistence.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.Diagnostics;

public sealed class PersistenceMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly PersistenceMetrics _metrics;

    public PersistenceMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new PersistenceMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    // ──── RecordEntityAudited ────

    [Fact]
    public void RecordEntityAudited_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.audited");

        _metrics.RecordEntityAudited("tenant-1", "Created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["operation"].ShouldBe("Created");
    }

    [Fact]
    public void RecordEntityAudited_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.audited");

        _metrics.RecordEntityAudited(null, "Modified");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordEntitySoftDeleted ────

    [Fact]
    public void RecordEntitySoftDeleted_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.soft_deleted");

        _metrics.RecordEntitySoftDeleted("tenant-2");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-2");
    }

    [Fact]
    public void RecordEntitySoftDeleted_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.soft_deleted");

        _metrics.RecordEntitySoftDeleted(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordConcurrencyStampGenerated ────

    [Fact]
    public void RecordConcurrencyStampGenerated_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.concurrency_stamp.generated");

        _metrics.RecordConcurrencyStampGenerated("tenant-3");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-3");
    }

    [Fact]
    public void RecordConcurrencyStampGenerated_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.concurrency_stamp.generated");

        _metrics.RecordConcurrencyStampGenerated(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordDomainEventDispatched ────

    [Fact]
    public void RecordDomainEventDispatched_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.domain_event.dispatched");

        _metrics.RecordDomainEventDispatched("tenant-4", "OrderCreatedEvent");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-4");
        snapshot[0].Tags["event_type"].ShouldBe("OrderCreatedEvent");
    }

    [Fact]
    public void RecordDomainEventDispatched_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.domain_event.dispatched");

        _metrics.RecordDomainEventDispatched(null, "TestEvent");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordIntegrationEventDispatched ────

    [Fact]
    public void RecordIntegrationEventDispatched_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.integration_event.dispatched");

        _metrics.RecordIntegrationEventDispatched("tenant-5", "UserCreatedEto");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-5");
        snapshot[0].Tags["event_type"].ShouldBe("UserCreatedEto");
    }

    [Fact]
    public void RecordIntegrationEventDispatched_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.integration_event.dispatched");

        _metrics.RecordIntegrationEventDispatched(null, "TestEto");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordEntitiesPurged ────

    [Fact]
    public void RecordEntitiesPurged_IncrementsWithCorrectCount()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.purged");

        _metrics.RecordEntitiesPurged("tenant-6", 42);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(42);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-6");
    }

    [Fact]
    public void RecordEntitiesPurged_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.entity.purged");

        _metrics.RecordEntitiesPurged(null, 7);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

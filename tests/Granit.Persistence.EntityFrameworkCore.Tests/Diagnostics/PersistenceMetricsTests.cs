// =============================================================================
// Tests - PersistenceMetrics
// =============================================================================
// Verifie que chaque methode Record* incremente le bon compteur avec les tags
// attendus (tenant_id, operation, event_type) et que le fallback "global"
// s'applique quand le tenant est null.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Persistence.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.Diagnostics;

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

    // ──── RecordCrossTenantQuery ────

    [Fact]
    public void RecordCrossTenantQuery_Implicit_TaggedByEntityAndOrigin()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        _metrics.RecordCrossTenantQuery("Invoice", "implicit");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["entity"].ShouldBe("Invoice");
        snapshot[0].Tags["origin"].ShouldBe("implicit");
    }

    [Fact]
    public void RecordCrossTenantQuery_Explicit_TaggedByEntityAndOrigin()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        _metrics.RecordCrossTenantQuery("Webhook", "explicit");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["entity"].ShouldBe("Webhook");
        snapshot[0].Tags["origin"].ShouldBe("explicit");
    }
}

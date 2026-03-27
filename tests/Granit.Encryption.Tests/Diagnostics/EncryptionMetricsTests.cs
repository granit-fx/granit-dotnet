// =============================================================================
// EncryptionMetricsTests - OpenTelemetry metrics for encryption module
// =============================================================================
// Verifies:
//   - Each Record* method increments the correct counter with expected tags
//   - Null tenantId is coalesced to "global"
//   - Non-null tenantId is passed through verbatim
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Encryption.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests.Diagnostics;

public sealed class EncryptionMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly EncryptionMetrics _metrics;

    public EncryptionMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new EncryptionMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    // ──── RecordKeyCreated ────

    [Fact]
    public void RecordKeyCreated_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.key.created");

        _metrics.RecordKeyCreated("tenant-42", "Patient");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
        snapshot[0].Tags["entity_type"].ShouldBe("Patient");
    }

    [Fact]
    public void RecordKeyCreated_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.key.created");

        _metrics.RecordKeyCreated(null, "Contract");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
        snapshot[0].Tags["entity_type"].ShouldBe("Contract");
    }

    [Fact]
    public void RecordKeyCreated_MultipleInvocations_IncrementsSeparately()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.key.created");

        _metrics.RecordKeyCreated("t1", "Patient");
        _metrics.RecordKeyCreated("t2", "Order");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(2);
    }

    // ──── RecordKeyShredded ────

    [Fact]
    public void RecordKeyShredded_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.key.shredded");

        _metrics.RecordKeyShredded("tenant-99", "Patient");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-99");
        snapshot[0].Tags["entity_type"].ShouldBe("Patient");
    }

    [Fact]
    public void RecordKeyShredded_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.key.shredded");

        _metrics.RecordKeyShredded(null, "Invoice");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── RecordShreddingError ────

    [Fact]
    public void RecordShreddingError_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.shredding.errors");

        _metrics.RecordShreddingError("tenant-7", "Patient");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-7");
        snapshot[0].Tags["entity_type"].ShouldBe("Patient");
    }

    [Fact]
    public void RecordShreddingError_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, EncryptionMetrics.MeterName, "granit.encryption.shredding.errors");

        _metrics.RecordShreddingError(null, "Document");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // ──── MeterName constant ────

    [Fact]
    public void MeterName_IsGranitEncryption() =>
        EncryptionMetrics.MeterName.ShouldBe("Granit.Encryption");
}

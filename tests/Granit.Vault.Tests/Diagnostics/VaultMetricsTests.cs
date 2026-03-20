using System.Diagnostics.Metrics;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests.Diagnostics;

public sealed class VaultMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly VaultMetrics _metrics;

    public VaultMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new VaultMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordOperationCompleted_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operations.completed");

        _metrics.RecordOperationCompleted("tenant-123", "encrypt", "aws", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["operation"].ShouldBe("encrypt");
        snapshot[0].Tags["provider"].ShouldBe("aws");
        snapshot[0].Tags["status"].ShouldBe("success");
    }

    [Fact]
    public void RecordOperationCompleted_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operations.completed");

        _metrics.RecordOperationCompleted(null, "decrypt", "azure", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordOperationError_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operations.errors");

        _metrics.RecordOperationError("tenant-456", "encrypt", "googlecloud");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-456");
        snapshot[0].Tags["operation"].ShouldBe("encrypt");
        snapshot[0].Tags["provider"].ShouldBe("googlecloud");
    }

    [Fact]
    public void RecordOperationError_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operations.errors");

        _metrics.RecordOperationError(null, "decrypt", "aws");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordOperationDuration_RecordsHistogram()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operation.duration");

        _metrics.RecordOperationDuration("tenant-789", "encrypt", "azure", TimeSpan.FromSeconds(1.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.5, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-789");
        snapshot[0].Tags["operation"].ShouldBe("encrypt");
        snapshot[0].Tags["provider"].ShouldBe("azure");
    }

    [Fact]
    public void RecordOperationDuration_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.operation.duration");

        _metrics.RecordOperationDuration(null, "decrypt", "googlecloud", TimeSpan.FromSeconds(0.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordRotationDetected_IncrementsWithProvider()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, VaultMetrics.MeterName, "granit.vault.rotations.detected");

        _metrics.RecordRotationDetected("aws");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["provider"].ShouldBe("aws");
    }
}

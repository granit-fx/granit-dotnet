using System.Diagnostics.Metrics;
using Granit.BlobStorage.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Diagnostics;

public sealed class BlobStorageMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly BlobStorageMetrics _metrics;

    public BlobStorageMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new BlobStorageMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordUploadInitiated_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.uploads.initiated");

        _metrics.RecordUploadInitiated("tenant-123", "images");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["container"].ShouldBe("images");
    }

    [Fact]
    public void RecordUploadInitiated_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.uploads.initiated");

        _metrics.RecordUploadInitiated(null, "docs");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordValidationCompleted_RecordsStatus()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.validations.completed");

        _metrics.RecordValidationCompleted("t1", "valid", "images");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["status"].ShouldBe("valid");
        snapshot[0].Tags["container"].ShouldBe("images");
    }

    [Fact]
    public void RecordValidationFailed_RecordsReason()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.validations.failed");

        _metrics.RecordValidationFailed("t1", "magic_bytes_mismatch", "uploads");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["reason"].ShouldBe("magic_bytes_mismatch");
    }

    [Fact]
    public void RecordDeleted_IncrementsCounter()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.blobs.deleted");

        _metrics.RecordDeleted("t1", "images");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
    }

    [Fact]
    public void RecordOrphanCleaned_IncrementsCounter()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.orphans.cleaned");

        _metrics.RecordOrphanCleaned("t1");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
    }

    [Fact]
    public void RecordConfirmDuration_RecordsHistogram()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, BlobStorageMetrics.MeterName, "granit.blobstorage.confirm.duration");

        _metrics.RecordConfirmDuration("t1", "valid", "images", TimeSpan.FromSeconds(2.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(2.5, 0.01);
        snapshot[0].Tags["status"].ShouldBe("valid");
    }
}

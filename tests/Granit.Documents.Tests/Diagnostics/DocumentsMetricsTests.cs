using System.Diagnostics.Metrics;
using Granit.Documents.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Diagnostics;

public sealed class DocumentsMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly DocumentsMetrics _metrics;

    public DocumentsMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new DocumentsMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordUpload_IncrementsWithTenantTag()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, DocumentsMetrics.MeterName, "granit.documents.upload.count");

        _metrics.RecordUpload("tenant-123");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
    }

    [Fact]
    public void RecordUpload_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, DocumentsMetrics.MeterName, "granit.documents.upload.count");

        _metrics.RecordUpload(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordDownload_Increments()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, DocumentsMetrics.MeterName, "granit.documents.download.count");

        _metrics.RecordDownload("t1");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public void RecordShareGranted_Increments()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, DocumentsMetrics.MeterName, "granit.documents.share.granted.count");

        _metrics.RecordShareGranted("t1");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public void RecordQuotaRejected_Increments()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, DocumentsMetrics.MeterName, "granit.documents.quota.rejected.count");

        _metrics.RecordQuotaRejected("t1");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public void Constructor_NullMeterFactory_Throws() =>
        Should.Throw<ArgumentNullException>(() => new DocumentsMetrics(null!));
}

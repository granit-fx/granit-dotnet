using System.Diagnostics.Metrics;
using Granit.LanguageDetection.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Tests.Diagnostics;

public sealed class LanguageDetectionMetricsTests
{
    [Fact]
    public void RecordHit_tags_detector_language_and_outcome()
    {
        using Harness h = new();
        using MetricCollector<long> collector = new(h.MeterFactory, LanguageDetectionMetrics.MeterName, "granit.language_detection.detections");

        h.Metrics.RecordHit("tenant-1", "trigram", "en");

        CollectedMeasurement<long> m = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        m.Value.ShouldBe(1);
        m.Tags["tenant_id"].ShouldBe("tenant-1");
        m.Tags["detector"].ShouldBe("trigram");
        m.Tags["result"].ShouldBe("hit");
        m.Tags["language"].ShouldBe("en");
    }

    [Fact]
    public void RecordMiss_uses_the_composite_detector_and_coalesces_a_null_tenant()
    {
        using Harness h = new();
        using MetricCollector<long> collector = new(h.MeterFactory, LanguageDetectionMetrics.MeterName, "granit.language_detection.detections");

        h.Metrics.RecordMiss(tenantId: null);

        CollectedMeasurement<long> m = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        m.Tags["tenant_id"].ShouldBe("global");
        m.Tags["detector"].ShouldBe("composite");
        m.Tags["result"].ShouldBe("miss");
    }

    [Fact]
    public void RecordLatency_records_the_duration_on_the_histogram()
    {
        using Harness h = new();
        using MetricCollector<double> collector = new(h.MeterFactory, LanguageDetectionMetrics.MeterName, "granit.language_detection.latency");

        h.Metrics.RecordLatency("tenant-1", 12.5);

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(12.5);
    }

    [Fact]
    public void Constructor_rejects_a_null_meter_factory()
    {
        Should.Throw<ArgumentNullException>(() => new LanguageDetectionMetrics(null!));
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _sp = new ServiceCollection().AddMetrics().BuildServiceProvider();

        public IMeterFactory MeterFactory => _sp.GetRequiredService<IMeterFactory>();
        public LanguageDetectionMetrics Metrics => field ??= new LanguageDetectionMetrics(MeterFactory);

        public void Dispose() => _sp.Dispose();
    }
}

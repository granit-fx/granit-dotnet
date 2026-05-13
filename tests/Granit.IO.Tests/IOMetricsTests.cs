using Granit.IO.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

[Collection("GranitIoMeter")]
public sealed class IOMetricsTests
{
    [Fact]
    public void Constructor_NullFactory_Throws() =>
        Should.Throw<ArgumentNullException>(() => new IOMetrics(null!));

    [Fact]
    public void RecordBytes_Histogram_DoesNotThrow()
    {
        using TestMeterFactory factory = new();
        IOMetrics metrics = new(factory);
        using MeterListenerHarness harness = new(IOMetrics.MeterName);

        metrics.RecordBytes("har", "abc", 1234);

        // No assert beyond "did not throw" — histogram fan-out is harness-dependent.
        Should.NotThrow(() => metrics.RecordBytes("har", null, 0));
    }

    [Fact]
    public void RecordJanitorPurged_Increments()
    {
        using TestMeterFactory factory = new();
        IOMetrics metrics = new(factory);
        using MeterListenerHarness harness = new(IOMetrics.MeterName);

        metrics.RecordJanitorPurged("age");

        harness.LongCounts("granit.io.temp.janitor.purged").ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void RecordCreated_Deleted_Increments()
    {
        using TestMeterFactory factory = new();
        IOMetrics metrics = new(factory);
        using MeterListenerHarness harness = new(IOMetrics.MeterName);

        metrics.RecordCreated("har", null);
        metrics.RecordCreated("har", "tenant-1");
        metrics.RecordDeleted("har", null);

        harness.LongCounts("granit.io.temp.created").ShouldBeGreaterThanOrEqualTo(2);
        harness.LongCounts("granit.io.temp.deleted").ShouldBeGreaterThanOrEqualTo(1);
    }
}

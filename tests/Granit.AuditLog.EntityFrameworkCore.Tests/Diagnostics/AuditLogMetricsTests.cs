using System.Diagnostics.Metrics;
using Granit.AuditLog.EntityFrameworkCore.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Diagnostics;

public sealed class AuditLogMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly MeterListener _listener;
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _recordings = [];

    public AuditLogMetricsTests()
    {
        _meterFactory = new TestMeterFactory();
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuditLogMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _recordings.Add((instrument.Name, measurement, tags.ToArray()));
        });
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
    }

    [Fact]
    public void MeterName_IsCorrect() => AuditLogMetrics.MeterName.ShouldBe("Granit.AuditLog");

    [Fact]
    public void RecordPersisted_RecordsCounter()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordPersisted(5, "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.entries.persisted" && r.Value == 5);
    }

    [Fact]
    public void RecordPersisted_WithNullTenant_UsesGlobal()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordPersisted(1, null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.entries.persisted" &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "global"));
    }

    [Fact]
    public void RecordPurged_RecordsCounter()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordPurged(100, "DataMutation", null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.entries.purged" && r.Value == 100);
    }

    [Fact]
    public void RecordPurged_IncludesCategoryTag()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordPurged(50, "ConfigurationChange", "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.entries.purged" &&
            r.Tags.Any(t => t.Key == "category" && (string?)t.Value == "ConfigurationChange"));
    }

    [Fact]
    public void RecordCaptureError_RecordsCounter()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordCaptureError("tenant-42");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.capture.errors" && r.Value == 1);
    }

    [Fact]
    public void RecordCaptureError_WithNullTenant_UsesGlobal()
    {
        AuditLogMetrics metrics = new(_meterFactory);

        metrics.RecordCaptureError(null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditlog.capture.errors" &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "global"));
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}

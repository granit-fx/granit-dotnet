using System.Diagnostics.Metrics;
using Granit.Auditing.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Diagnostics;

public sealed class AuditingMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly MeterListener _listener;
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _recordings = [];
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _doubleRecordings = [];
    private readonly List<(string Name, int Value, KeyValuePair<string, object?>[] Tags)> _intRecordings = [];

    public AuditingMetricsTests()
    {
        _meterFactory = new TestMeterFactory();
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuditingMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => _recordings.Add((instrument.Name, measurement, tags.ToArray())));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => _doubleRecordings.Add((instrument.Name, measurement, tags.ToArray())));
        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) => _intRecordings.Add((instrument.Name, measurement, tags.ToArray())));
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
    }

    [Fact]
    public void MeterName_IsCorrect() => AuditingMetrics.MeterName.ShouldBe("Granit.Auditing");

    [Fact]
    public void RecordPersisted_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordPersisted(5, "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.persisted" && r.Value == 5);
    }

    [Fact]
    public void RecordPersisted_WithNullTenant_UsesGlobal()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordPersisted(1, null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.persisted" &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "global"));
    }

    [Fact]
    public void RecordPurged_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordPurged(100, "DataMutation", null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.purged" && r.Value == 100);
    }

    [Fact]
    public void RecordPurged_IncludesCategoryTag()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordPurged(50, "ConfigurationChange", "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.purged" &&
            r.Tags.Any(t => t.Key == "category" && (string?)t.Value == "ConfigurationChange"));
    }

    [Fact]
    public void RecordCaptureError_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordCaptureError("tenant-42");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.capture.errors" && r.Value == 1);
    }

    [Fact]
    public void RecordCaptureError_WithNullTenant_UsesGlobal()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordCaptureError(null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.capture.errors" &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "global"));
    }

    [Fact]
    public void RecordPersistenceDuration_TagsMode()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordPersistenceDuration(12.5, "tenant-1", AuditingMetrics.EmbeddedMode);
        metrics.RecordPersistenceDuration(3.0, null, AuditingMetrics.StandaloneMode);
        _listener.RecordObservableInstruments();

        _doubleRecordings.ShouldContain(r =>
            r.Name == "granit.auditing.persistence.duration" &&
            r.Value == 12.5 &&
            r.Tags.Any(t => t.Key == "mode" && (string?)t.Value == "embedded"));
        _doubleRecordings.ShouldContain(r =>
            r.Name == "granit.auditing.persistence.duration" &&
            r.Value == 3.0 &&
            r.Tags.Any(t => t.Key == "mode" && (string?)t.Value == "standalone"));
    }

    [Fact]
    public void RecordEntityChangeCount_RecordsHistogram()
    {
        AuditingMetrics metrics = new(_meterFactory);

        metrics.RecordEntityChangeCount(7, "tenant-1");
        _listener.RecordObservableInstruments();

        _intRecordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.entity_changes" &&
            r.Value == 7 &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "tenant-1"));
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

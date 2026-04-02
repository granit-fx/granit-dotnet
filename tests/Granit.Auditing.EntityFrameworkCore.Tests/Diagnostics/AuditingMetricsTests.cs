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
    public void MeterName_IsCorrect() => AuditingMetrics.MeterName.ShouldBe("Granit.Auditing");

    [Fact]
    public void RecordPersisted_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordPersisted(5, "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.persisted" && r.Value == 5);
    }

    [Fact]
    public void RecordPersisted_WithNullTenant_UsesGlobal()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordPersisted(1, null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.persisted" &&
            r.Tags.Any(t => t.Key == "tenant_id" && (string?)t.Value == "global"));
    }

    [Fact]
    public void RecordPurged_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordPurged(100, "DataMutation", null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.purged" && r.Value == 100);
    }

    [Fact]
    public void RecordPurged_IncludesCategoryTag()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordPurged(50, "ConfigurationChange", "tenant-1");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.entry.purged" &&
            r.Tags.Any(t => t.Key == "category" && (string?)t.Value == "ConfigurationChange"));
    }

    [Fact]
    public void RecordCaptureError_RecordsCounter()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordCaptureError("tenant-42");
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.capture.errors" && r.Value == 1);
    }

    [Fact]
    public void RecordCaptureError_WithNullTenant_UsesGlobal()
    {
        AuditingMetrics metrics = new(_meterFactory, System.Threading.Channels.Channel.CreateUnbounded<Granit.Auditing.Messages.AuditingBatch>());

        metrics.RecordCaptureError(null);
        _listener.RecordObservableInstruments();

        _recordings.ShouldContain(r =>
            r.Name == "granit.auditing.capture.errors" &&
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

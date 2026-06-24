// =============================================================================
// Tests — TimelineMetrics
// =============================================================================
// Proves the meter actually emits: a real IMeterFactory + MeterListener capture
// each counter and its tags, so the registration seam has teeth rather than just
// resolving an object that never records.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Timeline.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineMetricsTests
{
    [Fact]
    public void RecordEntryPosted_emits_counter_with_tenant_entity_and_entry_type()
    {
        using TestMeterFactory factory = new();
        TimelineMetrics metrics = new(factory);
        using var capture = Capture.For("granit.timeline.entry.posted");

        metrics.RecordEntryPosted("tenant-1", "Patient", "Comment");

        capture.Total.ShouldBe(1);
        capture.Tags["tenant_id"].ShouldBe("tenant-1");
        capture.Tags["entity_type"].ShouldBe("Patient");
        capture.Tags["entry_type"].ShouldBe("Comment");
    }

    [Fact]
    public void RecordEntryDeleted_coalesces_null_tenant_to_global()
    {
        using TestMeterFactory factory = new();
        TimelineMetrics metrics = new(factory);
        using var capture = Capture.For("granit.timeline.entry.deleted");

        metrics.RecordEntryDeleted(tenantId: null, "Patient");

        capture.Total.ShouldBe(1);
        capture.Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordAnchorCreated_caps_cardinality_on_hostile_entity_type()
    {
        using TestMeterFactory factory = new();
        TimelineMetrics metrics = new(factory);
        using var capture = Capture.For("granit.timeline.anchor.created");

        metrics.RecordAnchorCreated("t", entityType: "DROP TABLE; --", sourceKey: "auditing");

        capture.Total.ShouldBe(1);
        capture.Tags["entity_type"].ShouldBe("_unknown_");
        capture.Tags["source_key"].ShouldBe("auditing");
    }

    // A real IMeterFactory (the NSubstitute one returns a null Meter, which would
    // never publish instruments to a listener).
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

    private sealed class Capture : IDisposable
    {
        private readonly MeterListener _listener;

        public long Total { get; private set; }

        public Dictionary<string, object?> Tags { get; } = new(StringComparer.Ordinal);

        private Capture(string instrumentName)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == TimelineMetrics.MeterName
                        && instrument.Name == instrumentName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            {
                Total += measurement;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    Tags[tag.Key] = tag.Value;
                }
            });
            _listener.Start();
        }

        public static Capture For(string instrumentName) => new(instrumentName);

        public void Dispose() => _listener.Dispose();
    }
}

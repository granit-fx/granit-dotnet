// =============================================================================
// QueryEngineMetricsTests - OpenTelemetry metrics for the query engine module
// =============================================================================
// Verifies:
//   - MeterName constant value
//   - RecordQueryExecuted increments counter with correct tags
//   - RecordStreamLimitReached increments counter with correct tags
//   - RecordQueryDuration records histogram with correct tags
//   - Null tenant ID is coalesced to "global"
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.QueryEngine.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Diagnostics;

public sealed class QueryEngineMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly QueryEngineMetrics _metrics;

    public QueryEngineMetricsTests()
    {
        _meterFactory = new TestMeterFactory();
        _metrics = new QueryEngineMetrics(_meterFactory);
    }

    public void Dispose() => (_meterFactory as IDisposable)?.Dispose();

    // ──── Constants ────

    [Fact]
    public void MeterName_IsGranitQueryEngine() =>
        QueryEngineMetrics.MeterName.ShouldBe("Granit.QueryEngine");

    // ──── RecordQueryExecuted ────

    [Fact]
    public void RecordQueryExecuted_IncrementsCounter()
    {
        using MeterListener listener = new();
        long recorded = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.executed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => recorded += measurement);
        listener.Start();

        _metrics.RecordQueryExecuted("tenant-1", "Product", "paged");
        listener.RecordObservableInstruments();

        recorded.ShouldBe(1);
    }

    [Fact]
    public void RecordQueryExecuted_IncludesCorrectTags()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.executed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordQueryExecuted("tenant-42", "Order", "stream");

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "tenant-42");
        capturedTags.ShouldContain(t => t.Key == "entity_type" && (string)t.Value! == "Order");
        capturedTags.ShouldContain(t => t.Key == "mode" && (string)t.Value! == "stream");
    }

    [Fact]
    public void RecordQueryExecuted_NullTenant_UsesGlobal()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.executed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordQueryExecuted(null, "Product", "paged");

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "global");
    }

    // ──── RecordStreamLimitReached ────

    [Fact]
    public void RecordStreamLimitReached_IncrementsCounter()
    {
        using MeterListener listener = new();
        long recorded = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.stream.limit_reached")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => recorded += measurement);
        listener.Start();

        _metrics.RecordStreamLimitReached("tenant-1", "Product");
        listener.RecordObservableInstruments();

        recorded.ShouldBe(1);
    }

    [Fact]
    public void RecordStreamLimitReached_IncludesCorrectTags()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.stream.limit_reached")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordStreamLimitReached("tenant-5", "Invoice");

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "tenant-5");
        capturedTags.ShouldContain(t => t.Key == "entity_type" && (string)t.Value! == "Invoice");
    }

    [Fact]
    public void RecordStreamLimitReached_NullTenant_UsesGlobal()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.stream.limit_reached")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordStreamLimitReached(null, "Product");

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "global");
    }

    // ──── RecordQueryDuration ────

    [Fact]
    public void RecordQueryDuration_RecordsHistogramValue()
    {
        using MeterListener listener = new();
        double recorded = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => recorded = measurement);
        listener.Start();

        _metrics.RecordQueryDuration("tenant-1", "Product", "paged", 0.456);

        recorded.ShouldBe(0.456);
    }

    [Fact]
    public void RecordQueryDuration_IncludesCorrectTags()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordQueryDuration("tenant-7", "Patient", "grouped", 1.234);

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "tenant-7");
        capturedTags.ShouldContain(t => t.Key == "entity_type" && (string)t.Value! == "Patient");
        capturedTags.ShouldContain(t => t.Key == "mode" && (string)t.Value! == "grouped");
    }

    [Fact]
    public void RecordQueryDuration_NullTenant_UsesGlobal()
    {
        using MeterListener listener = new();
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.query_engine.query.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            capturedTags = tags.ToArray());
        listener.Start();

        _metrics.RecordQueryDuration(null, "Product", "paged", 0.5);

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "global");
    }

    /// <summary>
    /// Minimal <see cref="IMeterFactory"/> for unit testing without
    /// Microsoft.Extensions.Diagnostics.Testing dependency.
    /// </summary>
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

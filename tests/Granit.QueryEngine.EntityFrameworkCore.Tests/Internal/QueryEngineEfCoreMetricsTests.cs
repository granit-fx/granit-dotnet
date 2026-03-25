using System.Diagnostics.Metrics;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEngineEfCoreMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly QueryEngineEfCoreMetrics _metrics;

    public QueryEngineEfCoreMetricsTests()
    {
        _meterFactory = new TestMeterFactory();
        _metrics = new QueryEngineEfCoreMetrics(_meterFactory);
    }

    public void Dispose() => (_meterFactory as IDisposable)?.Dispose();

    [Fact]
    public void RecordQueryExecuted_increments_counter()
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
    public void RecordQueryExecuted_uses_global_when_tenant_is_null()
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

    [Fact]
    public void RecordStreamLimitReached_increments_counter()
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
    public void RecordQueryDuration_records_histogram()
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

        _metrics.RecordQueryDuration("tenant-1", "Product", "paged", 0.123);

        recorded.ShouldBe(0.123);
    }

    [Fact]
    public void RecordQueryDuration_includes_correct_tags()
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

        _metrics.RecordQueryDuration("tenant-42", "Order", "stream", 1.5);

        capturedTags.ShouldNotBeNull();
        capturedTags.ShouldContain(t => t.Key == "tenant_id" && (string)t.Value! == "tenant-42");
        capturedTags.ShouldContain(t => t.Key == "entity_type" && (string)t.Value! == "Order");
        capturedTags.ShouldContain(t => t.Key == "mode" && (string)t.Value! == "stream");
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

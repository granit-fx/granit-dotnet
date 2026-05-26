using System.Diagnostics.Metrics;
using Granit.Indexing.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Tests;

public sealed class IndexingMetricsTests
{
    [Fact]
    public void Tenant_id_tag_falls_back_to_global_when_null()
    {
        using TestMeterFactory factory = new();
        List<KeyValuePair<string, object?>[]> tags = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = (i, l) =>
        {
            if (factory.Meters.Contains(i.Meter) && i.Name == "granit.indexing.entry.indexed")
            {
                l.EnableMeasurementEvents(i);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, t, _) => tags.Add(t.ToArray()));
        listener.Start();

        IndexingMetrics metrics = new(factory);
        metrics.RecordEntryIndexed(tenantId: null, backend: "ef");
        listener.Dispose();

        tags.Count.ShouldBe(1);
        tags[0].ShouldContain(kv => kv.Key == "tenant_id" && (string?)kv.Value == "global");
        tags[0].ShouldContain(kv => kv.Key == "backend" && (string?)kv.Value == "ef");
    }

    [Fact]
    public void Tenant_id_tag_uses_provided_value_when_present()
    {
        using TestMeterFactory factory = new();
        List<KeyValuePair<string, object?>[]> tags = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = (i, l) =>
        {
            if (factory.Meters.Contains(i.Meter) && i.Name == "granit.indexing.search.queries")
            {
                l.EnableMeasurementEvents(i);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, t, _) => tags.Add(t.ToArray()));
        listener.Start();

        IndexingMetrics metrics = new(factory);
        metrics.RecordSearchQuery(tenantId: "acme", backend: "ef");

        tags.Count.ShouldBe(1);
        tags[0].ShouldContain(kv => kv.Key == "tenant_id" && (string?)kv.Value == "acme");
    }

    [Fact]
    public void Authorization_filtered_counter_skipped_for_zero_delta()
    {
        // Avoid noisy zero-deltas in dashboards: the convention across Granit metrics is
        // not to emit a measurement when the delta is zero (or negative for counters).
        // Scope the listener to instruments from THIS factory's meter — DefaultSearchService
        // tests run in parallel and also publish to "granit.indexing.search.authorization_filtered"
        // (with non-zero deltas like 16/32/64), which would otherwise leak into the assertion.
        using TestMeterFactory factory = new();
        List<long> measurements = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = (i, l) =>
        {
            if (factory.Meters.Contains(i.Meter) && i.Name == "granit.indexing.search.authorization_filtered")
            {
                l.EnableMeasurementEvents(i);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, m, _, _) => measurements.Add(m));
        listener.Start();

        IndexingMetrics metrics = new(factory);
        metrics.RecordAuthorizationFiltered("acme", "ef", filteredCount: 0);
        metrics.RecordAuthorizationFiltered("acme", "ef", filteredCount: 5);

        measurements.ShouldBe([5]);
    }
}

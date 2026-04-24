using System.Diagnostics.Metrics;
using Granit.Catalog.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Catalog.Tests.Diagnostics;

public sealed class CatalogMetricsTests
{
    [Fact]
    public void MeterName_ShouldBe_GranitCatalog() =>
        CatalogMetrics.MeterName.ShouldBe("Granit.Catalog");

    [Fact]
    public void RecordProductCreated_ShouldEmit_OneCount()
    {
        CatalogMetrics metrics = BuildMetrics(out MeterListener rawListener, "granit.catalog.product.created", out List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> captured);
        using MeterListener listener = rawListener;

        metrics.RecordProductCreated(tenantId: "tenant-1");

        captured.ShouldHaveSingleItem();
        captured[0].Value.ShouldBe(1);
    }

    [Fact]
    public void RecordProductPublished_ShouldTagWithTenantId()
    {
        CatalogMetrics metrics = BuildMetrics(out MeterListener rawListener, "granit.catalog.product.published", out List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> captured);
        using MeterListener listener = rawListener;

        metrics.RecordProductPublished(tenantId: "tenant-42");

        captured.ShouldHaveSingleItem();
        captured[0].Tags.ShouldContain(t => t.Key == "tenant_id" && (string?)t.Value == "tenant-42");
    }

    [Fact]
    public void RecordProductPublished_WithNullTenant_ShouldUseGlobalTag()
    {
        CatalogMetrics metrics = BuildMetrics(out MeterListener rawListener, "granit.catalog.product.published", out List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> captured);
        using MeterListener listener = rawListener;

        metrics.RecordProductPublished(tenantId: null);

        captured.ShouldHaveSingleItem();
        captured[0].Tags.ShouldContain(t => t.Key == "tenant_id" && (string?)t.Value == "global");
    }

    [Fact]
    public void RecordExternalMappingAdded_ShouldTagWithProvider()
    {
        CatalogMetrics metrics = BuildMetrics(out MeterListener rawListener, "granit.catalog.product.external_mapping.added", out List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> captured);
        using MeterListener listener = rawListener;

        metrics.RecordExternalMappingAdded(tenantId: null, providerName: "stripe");

        captured.ShouldHaveSingleItem();
        captured[0].Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "stripe");
    }

    [Fact]
    public void RecordExternalMappingAdded_WithMissingProvider_ShouldThrow()
    {
        IMeterFactory factory = new ServiceCollection().AddMetrics().BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
        var metrics = new CatalogMetrics(factory);

        Should.Throw<ArgumentException>(() => metrics.RecordExternalMappingAdded(null, ""));
    }

    private static CatalogMetrics BuildMetrics(
        out MeterListener listener,
        string instrumentName,
        out List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> captured)
    {
        IMeterFactory factory = new ServiceCollection().AddMetrics().BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
        var metrics = new CatalogMetrics(factory);

        var localCaptured = new List<(long, IReadOnlyList<KeyValuePair<string, object?>>)>();
        captured = localCaptured;

        listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == CatalogMetrics.MeterName && instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            localCaptured.Add((value, tags.ToArray())));

        listener.Start();

        return metrics;
    }
}

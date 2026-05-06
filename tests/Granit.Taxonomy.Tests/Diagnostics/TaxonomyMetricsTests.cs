using System.Diagnostics.Metrics;
using Granit.Taxonomy.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Diagnostics;

public sealed class TaxonomyMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly TaxonomyMetrics _metrics;

    public TaxonomyMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new TaxonomyMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordTagCreated_IncrementsWithTenantAndScopeTags()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, TaxonomyMetrics.MeterName, "granit.taxonomy.tag.created");

        _metrics.RecordTagCreated("tenant-123", "documents");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-123");
        snapshot[0].Tags["scope"].ShouldBe("documents");
    }

    [Fact]
    public void RecordTagCreated_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, TaxonomyMetrics.MeterName, "granit.taxonomy.tag.created");

        _metrics.RecordTagCreated(null, "global");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
        snapshot[0].Tags["scope"].ShouldBe("global");
    }

    [Fact]
    public void RecordTagDeleted_Increments()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, TaxonomyMetrics.MeterName, "granit.taxonomy.tag.deleted");

        _metrics.RecordTagDeleted("tenant-1", "parties");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["scope"].ShouldBe("parties");
    }

    [Fact]
    public void Constructor_NullMeterFactory_Throws() =>
        Should.Throw<ArgumentNullException>(() => new TaxonomyMetrics(null!));
}

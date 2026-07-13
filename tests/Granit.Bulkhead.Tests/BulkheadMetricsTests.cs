using System.Diagnostics.Metrics;
using Granit.Bulkhead.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Tests;

public sealed class BulkheadMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly BulkheadMetrics _sut;

    public BulkheadMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _sut = new BulkheadMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void MeterName_IsCorrect() =>
        BulkheadMetrics.MeterName.ShouldBe("Granit.Bulkhead");

    [Fact]
    public void RecordAcquired_EmitsActiveLeaseIncrementWithPolicyAndTenantTags()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.leases.active");

        _sut.RecordAcquired("api", "tenant-1");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["policy"].ShouldBe("api");
        measurement.Tags["tenant_id"].ShouldBe("tenant-1");
    }

    [Fact]
    public void RecordAcquired_NullTenant_CoalescesTenantIdToGlobal()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.leases.active");

        _sut.RecordAcquired("api", null);

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordReleased_EmitsActiveLeaseDecrement()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.leases.active");

        _sut.RecordReleased("api", "tenant-1");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(-1);
        measurement.Tags["policy"].ShouldBe("api");
        measurement.Tags["tenant_id"].ShouldBe("tenant-1");
    }

    [Fact]
    public void RecordReleased_NullTenant_CoalescesTenantIdToGlobal()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.leases.active");

        _sut.RecordReleased("api", null);

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordRejected_EmitsRejectionIncrementWithPolicyAndTenantTags()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.requests.rejected");

        _sut.RecordRejected("api", "tenant-1");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["policy"].ShouldBe("api");
        measurement.Tags["tenant_id"].ShouldBe("tenant-1");
    }

    [Fact]
    public void RecordRejected_NullTenant_CoalescesTenantIdToGlobal()
    {
        using MetricCollector<long> collector = new(_meterFactory, BulkheadMetrics.MeterName, "granit.bulkhead.requests.rejected");

        _sut.RecordRejected("api", null);

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["tenant_id"].ShouldBe("global");
    }
}

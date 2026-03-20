using System.Diagnostics.Metrics;
using Granit.Identity.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Diagnostics;

public sealed class IdentityMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly IdentityMetrics _metrics;

    public IdentityMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new IdentityMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordOperationCompleted_IncrementsWithCorrectTags()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operations.completed");

        _metrics.RecordOperationCompleted("tenant-1", "get_user", "keycloak", "found");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["operation"].ShouldBe("get_user");
        snapshot[0].Tags["provider"].ShouldBe("keycloak");
        snapshot[0].Tags["status"].ShouldBe("found");
    }

    [Fact]
    public void RecordOperationCompleted_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operations.completed");

        _metrics.RecordOperationCompleted(null, "create_user", "keycloak", "created");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordOperationError_IncrementsWithCorrectTags()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operations.errors");

        _metrics.RecordOperationError("tenant-2", "get_user", "keycloak");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-2");
        snapshot[0].Tags["operation"].ShouldBe("get_user");
        snapshot[0].Tags["provider"].ShouldBe("keycloak");
    }

    [Fact]
    public void RecordOperationError_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operations.errors");

        _metrics.RecordOperationError(null, "update_user", "keycloak");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordOperationDuration_RecordsHistogram()
    {
        using MetricCollector<double> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operation.duration");

        _metrics.RecordOperationDuration("tenant-1", "get_user", "keycloak", TimeSpan.FromSeconds(1.25));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.25, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["operation"].ShouldBe("get_user");
        snapshot[0].Tags["provider"].ShouldBe("keycloak");
    }

    [Fact]
    public void RecordOperationDuration_NullTenant_UsesGlobal()
    {
        using MetricCollector<double> collector = new(
            _meterFactory, IdentityMetrics.MeterName, "granit.identity.operation.duration");

        _metrics.RecordOperationDuration(null, "create_user", "keycloak", TimeSpan.FromMilliseconds(500));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

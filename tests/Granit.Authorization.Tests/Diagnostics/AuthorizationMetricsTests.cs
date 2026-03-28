// =============================================================================
// AuthorizationMetricsTests - OpenTelemetry metrics for authorization module
// =============================================================================
// Verifies:
//   - Each Record* method increments the correct counter with expected tags
//   - Null tenant ID falls back to "global"
//   - Non-null tenant ID is passed through as-is
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Authorization.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Diagnostics;

public sealed class AuthorizationMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly AuthorizationMetrics _metrics;

    public AuthorizationMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new AuthorizationMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    // =========================================================================
    // RecordCheckGranted
    // =========================================================================

    [Fact]
    public void RecordCheckGranted_WithTenant_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.check.granted");

        _metrics.RecordCheckGranted("tenant-abc");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-abc");
    }

    [Fact]
    public void RecordCheckGranted_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.check.granted");

        _metrics.RecordCheckGranted(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordCheckGranted_MultipleCalls_AccumulatesCount()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.check.granted");

        _metrics.RecordCheckGranted("tenant-1");
        _metrics.RecordCheckGranted("tenant-1");
        _metrics.RecordCheckGranted("tenant-1");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(3);
        snapshot.All(m => m.Value == 1).ShouldBeTrue();
    }

    // =========================================================================
    // RecordCheckDenied
    // =========================================================================

    [Fact]
    public void RecordCheckDenied_WithTenant_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.check.denied");

        _metrics.RecordCheckDenied("tenant-xyz");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-xyz");
    }

    [Fact]
    public void RecordCheckDenied_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.check.denied");

        _metrics.RecordCheckDenied(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // =========================================================================
    // RecordCacheHit
    // =========================================================================

    [Fact]
    public void RecordCacheHit_WithTenant_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.cache.hit");

        _metrics.RecordCacheHit("tenant-42");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
    }

    [Fact]
    public void RecordCacheHit_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.cache.hit");

        _metrics.RecordCacheHit(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // =========================================================================
    // RecordCacheMiss
    // =========================================================================

    [Fact]
    public void RecordCacheMiss_WithTenant_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.cache.miss");

        _metrics.RecordCacheMiss("tenant-99");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-99");
    }

    [Fact]
    public void RecordCacheMiss_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, AuthorizationMetrics.MeterName, "granit.authorization.cache.miss");

        _metrics.RecordCacheMiss(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // =========================================================================
    // MeterName constant
    // =========================================================================

    [Fact]
    public void MeterName_IsGranitAuthorization() =>
        AuthorizationMetrics.MeterName.ShouldBe("Granit.Authorization");
}

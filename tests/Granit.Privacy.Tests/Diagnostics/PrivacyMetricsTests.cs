using System.Diagnostics.Metrics;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.Diagnostics;

public sealed class PrivacyMetricsTests : IDisposable
{
    // Fixed tenant ids so the expected tag strings are stable across runs.
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly PrivacyMetrics _metrics;

    public PrivacyMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new PrivacyMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordExportRequested_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.export.requests");

        _metrics.RecordExportRequested(TenantA);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe(TenantA.ToString());
    }

    [Fact]
    public void RecordExportRequested_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.export.requests");

        _metrics.RecordExportRequested(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordFragmentReceived_RecordsProviderTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.export.fragments.received");

        _metrics.RecordFragmentReceived(TenantA, "identity-provider");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe(TenantA.ToString());
        snapshot[0].Tags["provider"].ShouldBe("identity-provider");
    }

    [Fact]
    public void RecordDeletionRequested_IncrementsCounter()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.deletion.requests");

        _metrics.RecordDeletionRequested(TenantA);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
    }

    [Fact]
    public void RecordExportCompleted_RecordsHistogramWithStatus()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.export.duration");

        _metrics.RecordExportCompleted(TenantA, "completed", TimeSpan.FromSeconds(3.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(3.5, 0.01);
        snapshot[0].Tags["status"].ShouldBe("completed");
        snapshot[0].Tags["tenant_id"].ShouldBe(TenantA.ToString());
    }

    [Fact]
    public void RecordExportCompleted_TimeoutStatus_RecordsCorrectly()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, PrivacyMetrics.MeterName, "granit.privacy.export.duration");

        _metrics.RecordExportCompleted(null, "timeout", TimeSpan.Zero);

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(0.0);
        snapshot[0].Tags["status"].ShouldBe("timeout");
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

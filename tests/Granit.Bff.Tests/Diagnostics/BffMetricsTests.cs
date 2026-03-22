using System.Diagnostics.Metrics;
using Granit.Bff.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.Diagnostics;

public sealed class BffMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly BffMetrics _metrics;

    public BffMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new BffMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void Constructor_WithRealMeterFactory_DoesNotThrow()
    {
        // Already constructed in ctor — just verify it's not null
        _metrics.ShouldNotBeNull();
    }

    [Fact]
    public void MeterName_IsGranitBff() => BffMetrics.MeterName.ShouldBe("Granit.Bff");

    [Fact]
    public void RecordLogin_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.logins");

        _metrics.RecordLogin("tenant-42");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
    }

    [Fact]
    public void RecordLogin_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.logins");

        _metrics.RecordLogin(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordLogout_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.logouts");

        _metrics.RecordLogout("tenant-1");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
    }

    [Fact]
    public void RecordLogout_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.logouts");

        _metrics.RecordLogout(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordTokenRefresh_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.token.refreshes");

        _metrics.RecordTokenRefresh("tenant-5");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-5");
    }

    [Fact]
    public void RecordTokenRefresh_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.token.refreshes");

        _metrics.RecordTokenRefresh(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordProxyRequest_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.proxy.requests");

        _metrics.RecordProxyRequest("tenant-7");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-7");
    }

    [Fact]
    public void RecordProxyRequest_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.proxy.requests");

        _metrics.RecordProxyRequest(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordProxyError_IncrementsWithTenantAndReasonTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.proxy.errors");

        _metrics.RecordProxyError("tenant-3", "upstream_timeout");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-3");
        snapshot[0].Tags["reason"].ShouldBe("upstream_timeout");
    }

    [Fact]
    public void RecordProxyError_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.proxy.errors");

        _metrics.RecordProxyError(null, "missing_session");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
        snapshot[0].Tags["reason"].ShouldBe("missing_session");
    }

    [Fact]
    public void RecordCsrfRejection_IncrementsWithTenantTag()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.csrf.rejections");

        _metrics.RecordCsrfRejection("tenant-9");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-9");
    }

    [Fact]
    public void RecordCsrfRejection_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, BffMetrics.MeterName, "granit.bff.csrf.rejections");

        _metrics.RecordCsrfRejection(null);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

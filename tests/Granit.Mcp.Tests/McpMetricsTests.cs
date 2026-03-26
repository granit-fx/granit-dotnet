using System.Diagnostics.Metrics;
using Granit.Mcp.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;

namespace Granit.Mcp.Tests;

public sealed class McpMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly McpMetrics _sut;

    public McpMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _sut = new McpMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordToolInvoked_ShouldIncrementCounter()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, McpMetrics.MeterName, "granit.mcp.tools.invoked");

        _sut.RecordToolInvoked("tenant-1", "list_blobs", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-1");
        snapshot[0].Tags["tool_name"].ShouldBe("list_blobs");
        snapshot[0].Tags["status"].ShouldBe("success");
    }

    [Fact]
    public void RecordToolInvoked_WithNullTenant_ShouldDefaultToGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, McpMetrics.MeterName, "granit.mcp.tools.invoked");

        _sut.RecordToolInvoked(null, "echo", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordRequestDuration_ShouldRecordHistogram()
    {
        using MetricCollector<double> collector = new(
            _meterFactory, McpMetrics.MeterName, "granit.mcp.request.duration");

        _sut.RecordRequestDuration("tenant-1", "tools/call/echo", TimeSpan.FromMilliseconds(150));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].Value.ShouldBe(0.15, tolerance: 0.001);
        snapshot[0].Tags["method"].ShouldBe("tools/call/echo");
    }
}

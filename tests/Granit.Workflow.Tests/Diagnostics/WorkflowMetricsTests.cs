using System.Diagnostics.Metrics;
using Granit.Workflow.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests.Diagnostics;

public sealed class WorkflowMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly WorkflowMetrics _metrics;

    public WorkflowMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WorkflowMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordTransitionCompleted_IncrementsWithCorrectTags()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WorkflowMetrics.MeterName, "granit.workflow.transitions.completed");

        _metrics.RecordTransitionCompleted("tenant-42", "Completed", "Draft", "Published");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
        snapshot[0].Tags["outcome"].ShouldBe("Completed");
        snapshot[0].Tags["from_state"].ShouldBe("Draft");
        snapshot[0].Tags["to_state"].ShouldBe("Published");
    }

    [Fact]
    public void RecordTransitionCompleted_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, WorkflowMetrics.MeterName, "granit.workflow.transitions.completed");

        _metrics.RecordTransitionCompleted(null, "Denied", "Draft", "Published");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordTransitionDuration_RecordsHistogram()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, WorkflowMetrics.MeterName, "granit.workflow.transition.duration");

        _metrics.RecordTransitionDuration("tenant-42", "Completed", TimeSpan.FromSeconds(1.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.5, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
        snapshot[0].Tags["outcome"].ShouldBe("Completed");
    }

    [Fact]
    public void RecordTransitionDuration_NullTenant_UsesGlobal()
    {
        using var collector = new MetricCollector<double>(
            _meterFactory, WorkflowMetrics.MeterName, "granit.workflow.transition.duration");

        _metrics.RecordTransitionDuration(null, "Completed", TimeSpan.FromSeconds(0.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

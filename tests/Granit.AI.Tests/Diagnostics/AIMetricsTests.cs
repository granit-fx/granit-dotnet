using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.AI.Tests.Diagnostics;

public sealed class AIMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly AIMetrics _metrics;

    public AIMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new AIMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordRequestCompleted_IncrementsWithCorrectTags()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.requests.completed");

        _metrics.RecordRequestCompleted("tenant-42", "gpt-4o", "OpenAI", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-42");
        snapshot[0].Tags["model"].ShouldBe("gpt-4o");
        snapshot[0].Tags["provider"].ShouldBe("OpenAI");
        snapshot[0].Tags["status"].ShouldBe("success");
    }

    [Fact]
    public void RecordRequestCompleted_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.requests.completed");

        _metrics.RecordRequestCompleted(null, "gpt-4o", "OpenAI", "success");

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordTokensUsed_IncrementsInputAndOutputCounters()
    {
        using MetricCollector<long> inputCollector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.tokens.input");
        using MetricCollector<long> outputCollector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.tokens.output");

        _metrics.RecordTokensUsed("tenant-1", "claude-4", "Anthropic", 500, 200);

        IReadOnlyList<CollectedMeasurement<long>> inputSnapshot = inputCollector.GetMeasurementSnapshot();
        inputSnapshot.ShouldHaveSingleItem();
        inputSnapshot[0].Value.ShouldBe(500);
        inputSnapshot[0].Tags["model"].ShouldBe("claude-4");
        inputSnapshot[0].Tags["provider"].ShouldBe("Anthropic");

        IReadOnlyList<CollectedMeasurement<long>> outputSnapshot = outputCollector.GetMeasurementSnapshot();
        outputSnapshot.ShouldHaveSingleItem();
        outputSnapshot[0].Value.ShouldBe(200);
    }

    [Fact]
    public void RecordTokensUsed_NullTenant_UsesGlobal()
    {
        using MetricCollector<long> collector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.tokens.input");

        _metrics.RecordTokensUsed(null, "gpt-4o", "OpenAI", 100, 50);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordRequestDuration_RecordsHistogram()
    {
        using MetricCollector<double> collector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.request.duration");

        _metrics.RecordRequestDuration("tenant-7", "gpt-4o", "OpenAI", TimeSpan.FromSeconds(1.25));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1.25, 0.01);
        snapshot[0].Tags["tenant_id"].ShouldBe("tenant-7");
        snapshot[0].Tags["model"].ShouldBe("gpt-4o");
        snapshot[0].Tags["provider"].ShouldBe("OpenAI");
    }

    [Fact]
    public void RecordRequestDuration_NullTenant_UsesGlobal()
    {
        using MetricCollector<double> collector = new(
            _meterFactory, AIMetrics.MeterName, "granit.ai.request.duration");

        _metrics.RecordRequestDuration(null, "gpt-4o", "OpenAI", TimeSpan.FromSeconds(0.5));

        IReadOnlyList<CollectedMeasurement<double>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }
}

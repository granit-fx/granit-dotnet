using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.AI.Tools.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit AI tools orchestration loop.
/// Meter: <c>Granit.AI.Tools</c>.
/// </summary>
public sealed class AIToolsMetrics
{
    public const string MeterName = "Granit.AI.Tools";

    private readonly Counter<long> _invocations;
    private readonly Counter<long> _truncations;
    private readonly Histogram<int> _iterations;

    public AIToolsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _invocations = meter.CreateCounter<long>(
            "granit.ai.tools.invocations",
            description: "Number of tool calls executed by the orchestration loop.");

        _truncations = meter.CreateCounter<long>(
            "granit.ai.tools.truncations",
            description: "Number of tool results truncated to fit the context window.");

        _iterations = meter.CreateHistogram<int>(
            "granit.ai.tools.iterations",
            unit: "{iteration}",
            description: "Model round-trips per orchestration run.");
    }

    public void RecordInvocation(string? tenantId, string tool, string outcome) =>
        _invocations.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "tool", tool },
            { "outcome", outcome },
        });

    public void RecordTruncation(string? tenantId, string tool) =>
        _truncations.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "tool", tool },
        });

    public void RecordIterations(string? tenantId, int iterations) =>
        _iterations.Record(iterations, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}

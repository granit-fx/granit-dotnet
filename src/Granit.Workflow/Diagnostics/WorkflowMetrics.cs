using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Workflow.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the workflow module.
/// Meter: <c>Granit.Workflow</c>.
/// </summary>
public sealed class WorkflowMetrics
{
    public const string MeterName = "Granit.Workflow";

    private readonly Counter<long> _transitionsCompleted;
    private readonly Histogram<double> _transitionDuration;

    public WorkflowMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _transitionsCompleted = meter.CreateCounter<long>(
            "granit.workflow.transitions.completed",
            description: "Number of workflow transitions completed.");

        _transitionDuration = meter.CreateHistogram<double>(
            "granit.workflow.transition.duration",
            unit: "s",
            description: "Duration of workflow transitions in seconds.");
    }

    public void RecordTransitionCompleted(string? tenantId, string outcome, string fromState, string toState) =>
        _transitionsCompleted.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "outcome", outcome },
            { "from_state", fromState },
            { "to_state", toState },
        });

    public void RecordTransitionDuration(string? tenantId, string outcome, TimeSpan duration) =>
        _transitionDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "outcome", outcome },
        });
}

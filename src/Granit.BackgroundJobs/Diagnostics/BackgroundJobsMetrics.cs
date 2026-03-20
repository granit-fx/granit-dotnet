using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.BackgroundJobs.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the background jobs module.
/// Meter: <c>Granit.BackgroundJobs</c>.
/// </summary>
public sealed class BackgroundJobsMetrics
{
    public const string MeterName = "Granit.BackgroundJobs";

    private readonly Counter<long> _executionsCompleted;
    private readonly Histogram<double> _executionDuration;

    public BackgroundJobsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _executionsCompleted = meter.CreateCounter<long>(
            "granit.backgroundjobs.executions.completed",
            description: "Number of background job executions completed.");

        _executionDuration = meter.CreateHistogram<double>(
            "granit.backgroundjobs.execution.duration",
            unit: "s",
            description: "Duration of background job executions.");
    }

    /// <summary>
    /// Records a completed job execution (success or failure).
    /// </summary>
    public void RecordExecutionCompleted(string? tenantId, string jobName, string status)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "job_name", jobName },
            { "status", status },
        };
        _executionsCompleted.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of a job execution.
    /// </summary>
    public void RecordExecutionDuration(string? tenantId, string jobName, string status, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "job_name", jobName },
            { "status", status },
        };
        _executionDuration.Record(duration.TotalSeconds, tags);
    }
}

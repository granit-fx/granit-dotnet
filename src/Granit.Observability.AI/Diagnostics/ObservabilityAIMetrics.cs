using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Observability.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Observability AI module.
/// Meter: <c>Granit.Observability.AI</c>.
/// </summary>
public sealed class ObservabilityAIMetrics
{
    /// <summary>Meter name used for all Observability AI metrics.</summary>
    public const string MeterName = "Granit.Observability.AI";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _analysesCompleted;
    private readonly Counter<long> _analysesFailed;
    private readonly Histogram<double> _analysisDuration;

    public ObservabilityAIMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _analysesCompleted = meter.CreateCounter<long>(
            "granit.observability.ai.analysis.completed",
            description: "Number of log analyses completed successfully.");

        _analysesFailed = meter.CreateCounter<long>(
            "granit.observability.ai.analysis.failed",
            description: "Number of log analyses that failed or returned unparseable responses.");

        _analysisDuration = meter.CreateHistogram<double>(
            "granit.observability.ai.analysis.duration",
            unit: "s",
            description: "Duration of AI log analysis in seconds.");
    }

    /// <summary>Records a successfully completed log analysis.</summary>
    public void RecordAnalysisCompleted(string? tenantId, int insightCount, int entryCount) =>
        _analysesCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "insight_count", insightCount },
            { "entry_count", entryCount },
        });

    /// <summary>Records a failed or unparseable log analysis.</summary>
    public void RecordAnalysisFailed(string? tenantId, string reason) =>
        _analysesFailed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });

    /// <summary>Records the duration of a log analysis operation.</summary>
    public void RecordAnalysisDuration(string? tenantId, double durationSeconds) =>
        _analysisDuration.Record(durationSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}

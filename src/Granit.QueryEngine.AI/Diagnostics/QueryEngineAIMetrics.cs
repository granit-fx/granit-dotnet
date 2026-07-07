using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.QueryEngine.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the AI-powered natural language query translator.
/// Meter: <c>Granit.QueryEngine.AI</c>.
/// </summary>
public sealed class QueryEngineAIMetrics
{
    public const string MeterName = "Granit.QueryEngine.AI";

    private readonly Counter<long> _translationsExecuted;
    private readonly Counter<long> _translationsFailed;
    private readonly Histogram<double> _translationDuration;

    public QueryEngineAIMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _translationsExecuted = meter.CreateCounter<long>(
            "granit.query_engine.ai.translation.executed",
            description: "Number of NLQ translations executed.");

        _translationsFailed = meter.CreateCounter<long>(
            "granit.query_engine.ai.translation.failed",
            description: "Number of NLQ translations that failed (timeout, parse error, LLM error).");

        _translationDuration = meter.CreateHistogram<double>(
            "granit.query_engine.ai.translation.duration",
            unit: "s",
            description: "Duration of NLQ translation in seconds.");
    }

    public void RecordTranslationExecuted(string? tenantId)
    {
        // No outcome tag: failures are recorded on the dedicated `failed` counter, so an
        // outcome dimension here would be a constant ("success") — a dead cardinality.
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
        ];
        _translationsExecuted.Add(1, tags);
    }

    public void RecordTranslationFailed(string? tenantId, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("reason", reason),
        ];
        _translationsFailed.Add(1, tags);
    }

    public void RecordTranslationDuration(string? tenantId, double durationSeconds)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
        ];
        _translationDuration.Record(durationSeconds, tags);
    }
}

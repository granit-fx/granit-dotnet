using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Timeline.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for AI-powered timeline analysis.
/// Meter: <c>Granit.Timeline.AI</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.timeline.ai.{operation}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
public sealed class TimelineAIMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Timeline.AI";

    private const string TagTenantId = "tenant_id";
    private const string TagEntityType = "entity_type";
    private const string DefaultTenant = "global";
    private const string UnknownEntityType = "_unknown_";
    private const int MaxEntityTypeLength = 64;

    private readonly Meter _meter = meterFactory.Create(MeterName);

    private Counter<long>? _summarizationsCompleted;
    private Counter<long>? _summarizationFailures;
    private Histogram<double>? _summarizationDuration;
    private Counter<long>? _anomalyDetectionsCompleted;
    private Counter<long>? _anomalyDetectionFailures;
    private Histogram<double>? _anomalyDetectionDuration;
    private Counter<long>? _anomaliesFound;

    private Counter<long> SummarizationsCompleted => _summarizationsCompleted ??= _meter.CreateCounter<long>(
        "granit.timeline.ai.summarization.completed",
        description: "Number of AI timeline summarizations completed successfully.");

    private Counter<long> SummarizationFailures => _summarizationFailures ??= _meter.CreateCounter<long>(
        "granit.timeline.ai.summarization.failures",
        description: "Number of AI timeline summarizations that failed.");

    private Histogram<double> SummarizationDuration => _summarizationDuration ??= _meter.CreateHistogram<double>(
        "granit.timeline.ai.summarization.duration",
        unit: "s",
        description: "Duration of AI timeline summarization in seconds.");

    private Counter<long> AnomalyDetectionsCompleted => _anomalyDetectionsCompleted ??= _meter.CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.completed",
        description: "Number of AI timeline anomaly detections completed successfully.");

    private Counter<long> AnomalyDetectionFailures => _anomalyDetectionFailures ??= _meter.CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.failures",
        description: "Number of AI timeline anomaly detections that failed.");

    private Histogram<double> AnomalyDetectionDuration => _anomalyDetectionDuration ??= _meter.CreateHistogram<double>(
        "granit.timeline.ai.anomaly_detection.duration",
        unit: "s",
        description: "Duration of AI timeline anomaly detection in seconds.");

    private Counter<long> AnomaliesFound => _anomaliesFound ??= _meter.CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.anomalies_found",
        description: "Number of anomalies detected by AI analysis.");

    /// <summary>Records a completed AI timeline summarization.</summary>
    public void RecordSummarizationCompleted(string? tenantId, string entityType) =>
        SummarizationsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a failed AI timeline summarization.</summary>
    public void RecordSummarizationFailure(string? tenantId, string entityType) =>
        SummarizationFailures.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the duration of an AI timeline summarization.</summary>
    public void RecordSummarizationDuration(string? tenantId, string entityType, TimeSpan duration) =>
        SummarizationDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a completed AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionCompleted(string? tenantId, string entityType) =>
        AnomalyDetectionsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a failed AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionFailure(string? tenantId, string entityType) =>
        AnomalyDetectionFailures.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the duration of an AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionDuration(string? tenantId, string entityType, TimeSpan duration) =>
        AnomalyDetectionDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the number of anomalies found during detection.</summary>
    public void RecordAnomaliesFound(string? tenantId, string entityType, int count) =>
        AnomaliesFound.Add(count, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>
    /// Sanitizes entity type to prevent metrics cardinality explosion.
    /// Rejects values that are too long or contain non-alphanumeric characters.
    /// </summary>
    private static string SanitizeEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType) || entityType.Length > MaxEntityTypeLength)
        {
            return UnknownEntityType;
        }

        return entityType.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            ? entityType
            : UnknownEntityType;
    }
}

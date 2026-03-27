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

    private readonly Counter<long> _summarizationsCompleted = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.timeline.ai.summarization.completed",
        description: "Number of AI timeline summarizations completed successfully.");

    private readonly Counter<long> _summarizationFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.timeline.ai.summarization.failures",
        description: "Number of AI timeline summarizations that failed.");

    private readonly Histogram<double> _summarizationDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.timeline.ai.summarization.duration",
        unit: "s",
        description: "Duration of AI timeline summarization in seconds.");

    private readonly Counter<long> _anomalyDetectionsCompleted = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.completed",
        description: "Number of AI timeline anomaly detections completed successfully.");

    private readonly Counter<long> _anomalyDetectionFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.failures",
        description: "Number of AI timeline anomaly detections that failed.");

    private readonly Histogram<double> _anomalyDetectionDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.timeline.ai.anomaly_detection.duration",
        unit: "s",
        description: "Duration of AI timeline anomaly detection in seconds.");

    private readonly Counter<long> _anomaliesFound = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.timeline.ai.anomaly_detection.anomalies_found",
        description: "Number of anomalies detected by AI analysis.");

    /// <summary>Records a completed AI timeline summarization.</summary>
    public void RecordSummarizationCompleted(string? tenantId, string entityType) =>
        _summarizationsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a failed AI timeline summarization.</summary>
    public void RecordSummarizationFailure(string? tenantId, string entityType) =>
        _summarizationFailures.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the duration of an AI timeline summarization.</summary>
    public void RecordSummarizationDuration(string? tenantId, string entityType, TimeSpan duration) =>
        _summarizationDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a completed AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionCompleted(string? tenantId, string entityType) =>
        _anomalyDetectionsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records a failed AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionFailure(string? tenantId, string entityType) =>
        _anomalyDetectionFailures.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the duration of an AI timeline anomaly detection.</summary>
    public void RecordAnomalyDetectionDuration(string? tenantId, string entityType, TimeSpan duration) =>
        _anomalyDetectionDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>Records the number of anomalies found during detection.</summary>
    public void RecordAnomaliesFound(string? tenantId, string entityType, int count) =>
        _anomaliesFound.Add(count, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, SanitizeEntityType(entityType) },
        });

    /// <summary>
    /// Sanitizes entity type to prevent metrics cardinality explosion (VULN-211).
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

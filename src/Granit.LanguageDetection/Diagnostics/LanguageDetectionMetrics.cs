using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.LanguageDetection.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the language-detection module.
/// Meter: <c>Granit.LanguageDetection</c>.
/// </summary>
/// <remarks>
/// The <c>detector</c> tag is bounded to the static set of registered
/// <see cref="ILanguageDetectorProvider"/> type names plus the literal <c>"composite"</c>
/// — closed cardinality. The <c>language</c> tag (recorded on hits only) is bounded to
/// the ISO 639-1 alpha-2 code set (~90 values). <c>tenant_id</c> is coalesced to
/// <c>"global"</c> when no tenant context is active, matching the framework convention.
/// </remarks>
public sealed class LanguageDetectionMetrics
{
    public const string MeterName = "Granit.LanguageDetection";

    private readonly Counter<long> _detections;
    private readonly Histogram<double> _latency;

    public LanguageDetectionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _detections = meter.CreateCounter<long>(
            "granit.language_detection.detections",
            description: "Number of language-detection calls, tagged by detector and outcome (hit/miss).");

        _latency = meter.CreateHistogram<double>(
            "granit.language_detection.latency",
            unit: "ms",
            description: "End-to-end language-detection latency across the composite chain.");
    }

    /// <summary>Records a hit (a detector returned a non-null ISO 639-1 code).</summary>
    public void RecordHit(string? tenantId, string detector, string language)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("detector", detector),
            new("result", "hit"),
            new("language", language),
        ];
        _detections.Add(1, tags);
    }

    /// <summary>Records a miss (composite chain exhausted without a result).</summary>
    public void RecordMiss(string? tenantId)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("detector", "composite"),
            new("result", "miss"),
        ];
        _detections.Add(1, tags);
    }

    /// <summary>Records the composite-chain duration in milliseconds.</summary>
    public void RecordLatency(string? tenantId, double durationMilliseconds)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
        ];
        _latency.Record(durationMilliseconds, tags);
    }
}

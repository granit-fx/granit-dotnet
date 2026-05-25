using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.TextExtraction.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the text-extraction module.
/// Meter: <c>Granit.TextExtraction</c>.
/// </summary>
public sealed class TextExtractionMetrics
{
    public const string MeterName = "Granit.TextExtraction";

    private readonly Counter<long> _success;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _skipped;
    private readonly Counter<long> _truncated;

    public TextExtractionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _success = meter.CreateCounter<long>(
            "granit.text_extraction.extraction.success",
            description: "Number of successful text extractions.");

        _failed = meter.CreateCounter<long>(
            "granit.text_extraction.extraction.failed",
            description: "Number of failed text extractions (extractor threw or aborted).");

        _skipped = meter.CreateCounter<long>(
            "granit.text_extraction.extraction.skipped",
            description: "Number of extractions skipped because no extractor claimed the content type.");

        _truncated = meter.CreateCounter<long>(
            "granit.text_extraction.extraction.truncated",
            description: "Number of extractions that hit the MaxExtractedCharLength cap.");
    }

    public void RecordSuccess(string? tenantId, string extractorName, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", contentType),
        ];
        _success.Add(1, tags);
    }

    public void RecordFailed(string? tenantId, string extractorName, string contentType, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", contentType),
            new("reason", reason),
        ];
        _failed.Add(1, tags);
    }

    public void RecordSkipped(string? tenantId, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("content_type", contentType),
        ];
        _skipped.Add(1, tags);
    }

    public void RecordTruncated(string? tenantId, string extractorName, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", contentType),
        ];
        _truncated.Add(1, tags);
    }
}

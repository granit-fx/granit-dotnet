using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Mime;

namespace Granit.TextExtraction.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the text-extraction module.
/// Meter: <c>Granit.TextExtraction</c>.
/// </summary>
/// <remarks>
/// The <c>content_type</c> tag is normalised through <see cref="NormalizeContentType"/> before
/// it lands in any <see cref="TagList"/>: only the bare <c>type/subtype</c> survives, parameters
/// (<c>; charset=…</c>, <c>; boundary=…</c>) are dropped, and unparseable input degrades to a
/// fixed <c>invalid</c> sentinel. Without that step an attacker controlling the content type
/// could explode time-series cardinality on every counter (VULN-103, CWE-770).
/// </remarks>
public sealed class TextExtractionMetrics
{
    public const string MeterName = "Granit.TextExtraction";

    /// <summary>
    /// Sentinel surfaced on the <c>content_type</c> tag when the caller-supplied MIME string
    /// fails RFC 2045 parsing (malformed, empty, all-whitespace). Stable across releases so
    /// dashboards can alert on it.
    /// </summary>
    public const string InvalidContentTypeTag = "invalid";

    private readonly Counter<long> _success;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _skipped;
    private readonly Counter<long> _truncated;

    public TextExtractionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _success = meter.CreateCounter<long>(
            "granit.text_extraction.document.extracted",
            description: "Number of documents successfully extracted to text.");

        _failed = meter.CreateCounter<long>(
            "granit.text_extraction.document.failed",
            description: "Number of documents whose extraction failed (extractor threw or aborted).");

        _skipped = meter.CreateCounter<long>(
            "granit.text_extraction.document.skipped",
            description: "Number of documents skipped because no extractor claimed the content type.");

        _truncated = meter.CreateCounter<long>(
            "granit.text_extraction.document.truncated",
            description: "Number of documents whose extraction hit the MaxExtractedCharLength cap.");
    }

    public void RecordSuccess(string? tenantId, string extractorName, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", NormalizeContentType(contentType)),
        ];
        _success.Add(1, tags);
    }

    public void RecordFailed(string? tenantId, string extractorName, string contentType, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", NormalizeContentType(contentType)),
            new("reason", reason),
        ];
        _failed.Add(1, tags);
    }

    public void RecordSkipped(string? tenantId, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("content_type", NormalizeContentType(contentType)),
        ];
        _skipped.Add(1, tags);
    }

    public void RecordTruncated(string? tenantId, string extractorName, string contentType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("extractor", extractorName),
            new("content_type", NormalizeContentType(contentType)),
        ];
        _truncated.Add(1, tags);
    }

    /// <summary>
    /// Reduces a raw RFC 2045 content-type header to its <c>type/subtype</c> in lower case,
    /// dropping any <c>; param=value</c> trailers. Returns <see cref="InvalidContentTypeTag"/>
    /// when the input cannot be parsed.
    /// </summary>
    /// <remarks>
    /// Used as a metric-tag normaliser to bound cardinality. NOT a security check — extractors
    /// still see the raw caller-supplied MIME because the <c>;charset=…</c> piece can carry
    /// real semantics for parsers (e.g. text/plain with an explicit encoding).
    /// </remarks>
    public static string NormalizeContentType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return InvalidContentTypeTag;
        }

        try
        {
            // ContentType throws on any deviation from RFC 2045 (missing slash, illegal chars).
            ContentType parsed = new(raw);
            string? mediaType = parsed.MediaType;
            return string.IsNullOrWhiteSpace(mediaType)
                ? InvalidContentTypeTag
                : mediaType.ToLowerInvariant();
        }
        catch (FormatException)
        {
            return InvalidContentTypeTag;
        }
    }
}

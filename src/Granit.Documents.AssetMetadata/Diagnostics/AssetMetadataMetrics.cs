using System;
using System.Diagnostics.Metrics;

namespace Granit.Documents.AssetMetadata.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the asset-metadata module. Meter: <c>Granit.Documents.AssetMetadata</c>.
/// </summary>
public sealed class AssetMetadataMetrics
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Granit.Documents.AssetMetadata";

    private const string TagTenantId = "tenant_id";
    private const string TagSourceContentType = "source_content_type";
    private const string TagExtractor = "extractor";
    private const string TagErrorType = "error_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _extracted;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _gpsScrubbed;
    private readonly Histogram<double> _extractionDuration;

    /// <summary>Initialises the meter and instruments.</summary>
    public AssetMetadataMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        Meter meter = meterFactory.Create(MeterName);

        _extracted = meter.CreateCounter<long>(
            "granit.documents.asset_metadata.extracted.count",
            description: "Number of asset-metadata rows successfully populated.");
        _failed = meter.CreateCounter<long>(
            "granit.documents.asset_metadata.failed.count",
            description: "Number of asset-metadata extractions that failed terminally.");
        _gpsScrubbed = meter.CreateCounter<long>(
            "granit.documents.asset_metadata.gps_scrubbed.count",
            description: "Number of uploads whose GPS coordinates were stripped on ingest.");
        _extractionDuration = meter.CreateHistogram<double>(
            "granit.documents.asset_metadata.extraction.duration",
            unit: "ms",
            description: "Wall-clock duration of a single metadata extraction (one extractor run).");
    }

    /// <summary>Records a successful extraction.</summary>
    public void RecordExtracted(string? tenantId, string sourceContentType, string extractor) =>
        _extracted.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagSourceContentType, sourceContentType),
            new KeyValuePair<string, object?>(TagExtractor, extractor));

    /// <summary>Records a terminal extractor failure.</summary>
    public void RecordFailed(string? tenantId, string sourceContentType, string extractor, string errorType) =>
        _failed.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagSourceContentType, sourceContentType),
            new KeyValuePair<string, object?>(TagExtractor, extractor),
            new KeyValuePair<string, object?>(TagErrorType, errorType));

    /// <summary>Records a GPS-scrub on upload.</summary>
    public void RecordGpsScrubbed(string? tenantId, string sourceContentType) =>
        _gpsScrubbed.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagSourceContentType, sourceContentType));

    /// <summary>Records the wall-clock duration of a single extractor run (ms).</summary>
    public void RecordExtractionDuration(string? tenantId, string extractor, double durationMs) =>
        _extractionDuration.Record(durationMs,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagExtractor, extractor));
}

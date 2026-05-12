using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Documents.Renditions.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the renditions module. Meter: <c>Granit.Documents.Renditions</c>.
/// </summary>
internal sealed class RenditionsMetrics
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Granit.Documents.Renditions";

    private const string TagTenantId = "tenant_id";
    private const string TagSourceContentType = "source_content_type";
    private const string TagTargetContentType = "target_content_type";
    private const string TagRenditionType = "rendition_type";
    private const string TagProvider = "provider";
    private const string TagErrorType = "error_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _generated;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _onDemandServed;
    private readonly Histogram<double> _generationDuration;

    /// <summary>Initialises the meter and instruments.</summary>
    public RenditionsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        Meter meter = meterFactory.Create(MeterName);

        _generated = meter.CreateCounter<long>(
            "granit.documents.renditions.generated.count",
            description: "Number of renditions successfully generated.");
        _failed = meter.CreateCounter<long>(
            "granit.documents.renditions.failed.count",
            description: "Number of renditions whose generation pipeline failed terminally.");
        _onDemandServed = meter.CreateCounter<long>(
            "granit.documents.renditions.on_demand_served.count",
            description: "Number of renditions generated synchronously through the on-demand download fallback.");
        _generationDuration = meter.CreateHistogram<double>(
            "granit.documents.renditions.generation.duration",
            unit: "s",
            description: "Wall-clock duration of the rendition generation pipeline (all hops).");
    }

    /// <summary>Records a successful generation.</summary>
    public void RecordGenerated(string? tenantId, string sourceContentType, string targetContentType, string renditionType) =>
        _generated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagSourceContentType, sourceContentType },
            { TagTargetContentType, targetContentType },
            { TagRenditionType, renditionType },
        });

    /// <summary>Records a failed generation.</summary>
    public void RecordFailed(string? tenantId, string sourceContentType, string targetContentType, string errorType) =>
        _failed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagSourceContentType, sourceContentType },
            { TagTargetContentType, targetContentType },
            { TagErrorType, errorType },
        });

    /// <summary>Records an on-demand fallback hit (rendition was missing and generated synchronously on download).</summary>
    public void RecordOnDemandServed(string? tenantId, string targetContentType, string renditionType) =>
        _onDemandServed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTargetContentType, targetContentType },
            { TagRenditionType, renditionType },
        });

    /// <summary>Records the duration of a pipeline run.</summary>
    public void RecordGenerationDuration(string sourceContentType, string targetContentType, string provider, TimeSpan duration) =>
        _generationDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagSourceContentType, sourceContentType },
            { TagTargetContentType, targetContentType },
            { TagProvider, provider },
        });
}

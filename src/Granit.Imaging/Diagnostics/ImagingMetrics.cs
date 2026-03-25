using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Imaging.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the imaging module.
/// Meter: <c>Granit.Imaging</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.imaging.{entity}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
public sealed class ImagingMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Imaging";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _imagesProcessed = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.imaging.image.processed",
        description: "Number of images processed through the pipeline.");

    private readonly Histogram<double> _processingDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.imaging.image.processing_duration",
        unit: "s",
        description: "Duration of image processing in seconds.");

    /// <summary>Records a completed image processing operation.</summary>
    public void RecordImageProcessed(string? tenantId, string outputFormat) =>
        _imagesProcessed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "output_format", outputFormat },
        });

    /// <summary>Records the duration of an image processing pipeline execution.</summary>
    public void RecordProcessingDuration(string? tenantId, string outputFormat, TimeSpan duration) =>
        _processingDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "output_format", outputFormat },
        });
}

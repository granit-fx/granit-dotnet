using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Imaging.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for AI-powered image analysis.
/// Meter: <c>Granit.Imaging.AI</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.imaging.analysis.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
public sealed class ImagingAIMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Imaging.AI";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _analysesCompleted = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.imaging.analysis.completed",
        description: "Number of AI image analyses completed successfully.");

    private readonly Counter<long> _analysesFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.imaging.analysis.failures",
        description: "Number of AI image analyses that failed.");

    private readonly Histogram<double> _analysisDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.imaging.analysis.duration",
        unit: "s",
        description: "Duration of AI image analysis in seconds.");

    /// <summary>Records a completed AI image analysis.</summary>
    public void RecordAnalysisCompleted(string? tenantId, string contentType) =>
        _analysesCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "content_type", contentType },
        });

    /// <summary>Records a failed AI image analysis.</summary>
    public void RecordAnalysisFailure(string? tenantId, string contentType) =>
        _analysesFailures.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "content_type", contentType },
        });

    /// <summary>Records the duration of an AI image analysis.</summary>
    public void RecordAnalysisDuration(string? tenantId, string contentType, TimeSpan duration) =>
        _analysisDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "content_type", contentType },
        });
}

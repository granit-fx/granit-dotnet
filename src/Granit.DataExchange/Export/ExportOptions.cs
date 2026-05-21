using System.ComponentModel.DataAnnotations;

namespace Granit.DataExchange.Export;

/// <summary>
/// Configuration options for the data export pipeline.
/// </summary>
/// <remarks>
/// Bound to the <c>DataExchange:Export</c> configuration section.
/// </remarks>
public sealed class ExportOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "DataExchange:Export";

    /// <summary>
    /// Row count threshold above which the export is dispatched to a background job.
    /// Default: <c>1000</c>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int BackgroundThreshold { get; set; } = 1000;
}

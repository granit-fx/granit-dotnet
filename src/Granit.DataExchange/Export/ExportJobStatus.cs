namespace Granit.DataExchange.Export;

/// <summary>
/// Status of an export job throughout its lifecycle.
/// </summary>
public enum ExportJobStatus
{
    /// <summary>Job created and queued for background execution.</summary>
    Queued,

    /// <summary>Export is currently being generated.</summary>
    Exporting,

    /// <summary>Export completed successfully — file available for download.</summary>
    Completed,

    /// <summary>Export failed.</summary>
    Failed,
}

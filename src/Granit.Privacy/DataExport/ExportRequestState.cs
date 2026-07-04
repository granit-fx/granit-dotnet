namespace Granit.Privacy.DataExport;

/// <summary>
/// State of a personal data export request (Art. 15/20).
/// </summary>
public enum ExportRequestState
{
    /// <summary>Request submitted, scatter-gather saga in progress.</summary>
    Pending,

    /// <summary>All data providers responded — archive is ready for download.</summary>
    Completed,

    /// <summary>Saga timed out but some providers responded — partial archive available.</summary>
    PartiallyCompleted,

    /// <summary>Saga timed out with no provider responses.</summary>
    TimedOut,

    /// <summary>
    /// Archive assembly aborted because the ZIP exceeded
    /// <see cref="Options.GranitPrivacyOptions.ExportMaxSizeMb"/>.
    /// </summary>
    SizeLimitExceeded,
}

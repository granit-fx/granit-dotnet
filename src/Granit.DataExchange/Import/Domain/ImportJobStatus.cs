namespace Granit.DataExchange.Import.Domain;

/// <summary>
/// Status of an import job throughout its lifecycle.
/// </summary>
public enum ImportJobStatus
{
    /// <summary>File uploaded, job created.</summary>
    Created,

    /// <summary>Headers extracted, preview and mapping suggestions generated.</summary>
    Previewed,

    /// <summary>Column mappings confirmed by the user.</summary>
    Mapped,

    /// <summary>Import is currently executing (Wolverine background handler).</summary>
    Executing,

    /// <summary>All rows imported successfully.</summary>
    Completed,

    /// <summary>Some rows failed but others succeeded.</summary>
    PartiallyCompleted,

    /// <summary>Import failed entirely.</summary>
    Failed,

    /// <summary>Import was cancelled by the user.</summary>
    Cancelled,
}

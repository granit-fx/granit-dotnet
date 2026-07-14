namespace Granit.DataExchange.Import;

/// <summary>
/// Configuration options for the data exchange import subsystem.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DataExchange:Import";

    /// <summary>
    /// Default maximum file size in megabytes.
    /// Can be overridden per import definition via <c>ImportDefinition&lt;T&gt;.MaxFileSizeMb</c>.
    /// Default: <c>50</c>.
    /// </summary>
    public int DefaultMaxFileSizeMb { get; set; } = 50;

    /// <summary>
    /// Default batch size for import execution.
    /// Default: <c>500</c>.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 500;

    /// <summary>
    /// Minimum fuzzy matching score (0.0 to 1.0) for the fuzzy tier of mapping suggestions.
    /// Default: <c>0.8</c>.
    /// </summary>
    public double FuzzyMatchThreshold { get; set; } = 0.8;

    /// <summary>
    /// When <see langword="true"/>, the uploaded source file is deleted as soon as the import
    /// completes with zero failed rows — nothing is left to correct, so the file no longer needs
    /// to be retained until the next retention sweep (GDPR Art. 5(1)(e) storage limitation).
    /// Imports with at least one failed row keep the file so the correction-file endpoint can
    /// still regenerate it. Opt-in; default <see langword="false"/> to preserve prior behavior.
    /// </summary>
    public bool DeleteUploadedFileOnSuccess { get; set; }
}

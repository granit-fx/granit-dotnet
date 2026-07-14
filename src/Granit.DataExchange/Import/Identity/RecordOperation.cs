namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// The persistence operation to perform for an imported record.
/// </summary>
public enum RecordOperation
{
    /// <summary>The record does not exist — perform an INSERT.</summary>
    Insert,

    /// <summary>The record already exists — perform an UPDATE.</summary>
    Update,

    /// <summary>INSERT if the record does not exist, UPDATE if it does — resolved at persistence time.</summary>
    Upsert,

    /// <summary>The row must not be persisted (e.g. a duplicate key already seen in the same file).</summary>
    Skip,

    /// <summary>The row's identity could not be resolved unambiguously (e.g. a missing key component).</summary>
    Ambiguous,
}

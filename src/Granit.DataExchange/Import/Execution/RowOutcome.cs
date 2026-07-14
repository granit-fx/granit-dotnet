using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Reporting;

namespace Granit.DataExchange.Import.Execution;

/// <summary>
/// Outcome of one source row after mapping, validation, and identity resolution —
/// errors travel as data so the executor can count and report them instead of
/// rows silently disappearing from the pipeline.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed record RowOutcome<TEntity> where TEntity : class
{
    /// <summary>One-based row number in the source file.</summary>
    public required int RowNumber { get; init; }

    /// <summary>The mapped entity. Non-null only for successful outcomes.</summary>
    public TEntity? Entity { get; init; }

    /// <summary>The identity resolution result, or <c>null</c> for insert-only imports.</summary>
    public RecordIdentity? Identity { get; init; }

    /// <summary>The row-level error. Non-null only for failed outcomes.</summary>
    public ImportRowError? Error { get; init; }

    /// <summary>Whether the row was skipped (every mapped cell empty).</summary>
    public bool IsSkipped { get; init; }

#pragma warning disable CA1000 // Intentional: typed factory methods keep the outcome states (Ok/Failed/Skipped) self-validating
    /// <summary>
    /// Creates a successful outcome ready for persistence.
    /// </summary>
    /// <param name="rowNumber">One-based source row number.</param>
    /// <param name="entity">The mapped, validated entity.</param>
    /// <param name="identity">Optional identity resolution result.</param>
    public static RowOutcome<TEntity> Ok(int rowNumber, TEntity entity, RecordIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new() { RowNumber = rowNumber, Entity = entity, Identity = identity };
    }

    /// <summary>
    /// Creates a failed outcome carrying the row error to the report.
    /// </summary>
    /// <param name="rowNumber">One-based source row number.</param>
    /// <param name="error">The row-level error.</param>
    public static RowOutcome<TEntity> Failed(int rowNumber, ImportRowError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new() { RowNumber = rowNumber, Error = error };
    }

    /// <summary>
    /// Creates a skipped outcome (e.g. an all-empty row).
    /// </summary>
    /// <param name="rowNumber">One-based source row number.</param>
    public static RowOutcome<TEntity> Skipped(int rowNumber) =>
        new() { RowNumber = rowNumber, IsSkipped = true };
#pragma warning restore CA1000
}

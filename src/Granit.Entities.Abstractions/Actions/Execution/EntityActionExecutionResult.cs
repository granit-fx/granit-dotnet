namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Per-row outcome of <see cref="IEntityActionExecutor{TEntity}.ExecuteAsync"/>.
/// Exactly one of <see cref="AffectedEntity"/> / <see cref="FailureReason"/> is
/// non-<see langword="null"/> on a well-behaved implementation.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="AffectedEntity">Post-save entity when the row succeeded; <see langword="null"/> on failure.</param>
/// <param name="FailureReason">Human-readable failure reason (forwarded into <see cref="BulkActionFailure.Reason"/>); <see langword="null"/> on success.</param>
public sealed record EntityActionExecutionResult<TEntity>(
    TEntity? AffectedEntity,
    string? FailureReason)
    where TEntity : class;

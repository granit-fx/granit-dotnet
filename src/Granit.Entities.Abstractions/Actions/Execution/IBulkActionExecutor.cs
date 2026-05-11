using System.Text.Json;

namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Optional bulk-friendly counterpart to <see cref="IEntityActionExecutor{TEntity}"/>
/// (ADR-056 §Q4). When registered, the bulk endpoint dispatches the whole list
/// in one call (e.g. one <c>UPDATE … WHERE Id IN (…)</c>). When missing, the
/// framework's <c>BulkActionExecutionOrchestrator</c> loops the per-row executor
/// — same observable behaviour, N queries.
/// </summary>
/// <typeparam name="TEntity">The entity the action targets.</typeparam>
public interface IBulkActionExecutor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Executes the action against the rows identified by <paramref name="ids"/>.
    /// </summary>
    /// <param name="ids">Primary keys of the targeted rows. Always non-empty; the framework guards an empty input.</param>
    /// <param name="payload">Free-form JSON payload supplied by the caller.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    /// The set of affected (post-save) entities plus a structured per-row failure
    /// list. Hosts MUST return one failure per missing / rejected id so the caller
    /// can re-issue against the failed rows.
    /// </returns>
    Task<BulkActionExecutionResult<TEntity>> ExecuteBulkAsync(
        IReadOnlyList<Guid> ids,
        JsonElement payload,
        CancellationToken cancellationToken);
}

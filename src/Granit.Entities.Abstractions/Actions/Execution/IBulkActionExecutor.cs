using System.Text.Json;

namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Optional bulk-optimized action executor. When registered for an action,
/// the framework uses this instead of looping per-entity via
/// <see cref="IEntityActionExecutor{TEntity}"/>. Allows atomicity, transaction
/// batching, and bulk-specific optimizations (e.g., bulk UPDATE SQL).
/// </summary>
/// <remarks>
/// Not implementing this interface does not prevent bulk operations — the
/// framework falls back to per-entity <see cref="IEntityActionExecutor{TEntity}"/>
/// calls. Register this interface only when the action's logic benefits from
/// seeing all affected entities at once (e.g., cascading updates, audit trails
/// spanning multiple rows, or database-side bulk operations).
/// </remarks>
/// <typeparam name="TEntity">The entity type this executor acts upon.</typeparam>
public interface IBulkActionExecutor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Executes the action against a batch of entity instances. All entities are
    /// materialized from the database before this call. The executor owns
    /// transaction lifecycle and side effects.
    /// </summary>
    /// <param name="entities">All entities targeted by the bulk operation.
    /// Read-only collection to prevent accidental modification outside executor
    /// control. Executor must materialize its own mutable copies if updates
    /// are needed.</param>
    /// <param name="payload">Client-provided action parameters as a JSON element.
    /// Same semantics as <see cref="IEntityActionExecutor{TEntity}.ExecuteAsync"/>:
    /// opaque to framework, shape validation is executor responsibility.</param>
    /// <param name="cancellationToken">Cancellation signal for async work.</param>
    /// <returns>
    /// <see cref="BulkActionResult"/> containing the count of successfully
    /// affected rows and a list of per-row failures (if any). On partial success,
    /// committed rows are reported and failures listed separately.
    /// </returns>
    Task<BulkActionResult> ExecuteBulkAsync(
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken);
}

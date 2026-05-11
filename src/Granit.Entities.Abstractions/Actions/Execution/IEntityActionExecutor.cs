using System.Text.Json;

namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Server-side execution surface for one entity action (ADR-056). Implementations
/// mutate a single row identified by its primary key and return the post-save
/// entity so the framework can emit a single batched
/// <see cref="Granit.Events.EntityBulkUpdatedEvent{TEntity}"/> for the run.
/// </summary>
/// <typeparam name="TEntity">The entity the action targets. Must match the
/// <c>EntityDefinition{TEntity}</c>'s entity type.</typeparam>
/// <remarks>
/// <para>
/// Executors are resolved scoped per request (the typical implementation needs
/// a <c>DbContext</c>). Hosts register the executor explicitly via
/// <c>services.AddScoped&lt;TExecutor&gt;()</c>; the framework does not auto-register
/// it — the fluent <c>.ServerExecutor&lt;TExecutor&gt;()</c> builder only captures the
/// type on the descriptor.
/// </para>
/// <para>
/// Row addressing is by <see cref="Guid"/> id — the executor owns the load,
/// including tenant / soft-delete / Named-Query-Filter application via the
/// host's <c>DbContext</c>. Rows the executor cannot find or chooses to
/// reject for row-level visibility reasons (defense in depth) must be
/// reported back through <see cref="EntityActionExecutionResult{TEntity}.FailureReason"/>.
/// </para>
/// </remarks>
public interface IEntityActionExecutor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Executes the action against the row identified by <paramref name="id"/>.
    /// Returns the post-save entity on success and <see langword="null"/> with a
    /// <see cref="EntityActionExecutionResult{TEntity}.FailureReason"/> when the
    /// row was rejected (not found, permission denied, invariant violated, …).
    /// </summary>
    /// <param name="id">Primary key of the targeted row.</param>
    /// <param name="payload">Free-form JSON payload supplied by the caller. May be <see cref="JsonElement.ValueKind"/> <c>Undefined</c> when the action takes no parameters.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task<EntityActionExecutionResult<TEntity>> ExecuteAsync(
        Guid id,
        JsonElement payload,
        CancellationToken cancellationToken);
}

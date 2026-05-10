using System.Text.Json;

namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Server-side action execution contract. Implementers handle the business logic
/// triggered by an action — from simple updates to complex workflows, permissions
/// checks, and side effects. Each executor is generic over the target entity type.
/// </summary>
/// <remarks>
/// Implementations are discovered via DI and registered per action via
/// <see cref="EntityActionBuilder{TEntity}.ServerExecutor{TExecutor}()"/>.
/// The framework guarantees the entity instance passed to <see cref="ExecuteAsync"/>
/// matches the declared generic type.
/// </remarks>
/// <typeparam name="TEntity">The entity type this executor acts upon.</typeparam>
public interface IEntityActionExecutor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Executes the action against a single entity instance. The payload is parsed
    /// from the client request and opaque to the framework — interpreting its
    /// structure is the executor's responsibility.
    /// </summary>
    /// <param name="entity">The entity instance to act upon. Materialized from the
    /// database and ready for modification. Entity state changes (e.g., property
    /// mutations) are NOT automatically saved — the executor owns transaction
    /// orchestration via injected <c>IUnitOfWork</c> or DbContext.</param>
    /// <param name="payload">Client-provided action parameters as a JSON element.
    /// May be an empty object (<c>{}</c>). Null checks and shape validation are
    /// the executor's responsibility.</param>
    /// <param name="cancellationToken">Cancellation signal for async work.</param>
    /// <returns>
    /// <see cref="ActionResult"/> indicating success or failure. On success,
    /// the executor should have committed all changes. On failure, the framework
    /// records the error and includes it in the bulk response.
    /// </returns>
    Task<ActionResult> ExecuteAsync(TEntity entity, JsonElement payload, CancellationToken cancellationToken);
}

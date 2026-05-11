namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Aggregate outcome of <see cref="IBulkActionExecutor{TEntity}.ExecuteBulkAsync"/>
/// or the framework's per-row loop fallback. Carries both the affected entities
/// (so the framework can emit a single
/// <see cref="Granit.Events.EntityBulkUpdatedEvent{TEntity}"/>) and the structured
/// failure list (so the caller can re-issue against rejected rows).
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="AffectedEntities">Post-save entities for the rows the executor accepted. Empty when nothing succeeded.</param>
/// <param name="Failures">One entry per rejected row.</param>
public sealed record BulkActionExecutionResult<TEntity>(
    IReadOnlyList<TEntity> AffectedEntities,
    IReadOnlyList<BulkActionFailure> Failures)
    where TEntity : class;

using Granit.Domain;

namespace Granit.Events;

/// <summary>
/// Domain event signalling that a batch of <typeparamref name="TEntity"/> rows
/// was created, updated, or deleted in a single host operation (story #1794).
/// Hosts emit this once per bulk operation instead of N per-row
/// <see cref="EntityCreatedEvent{TEntity}"/> / <see cref="EntityUpdatedEvent{TEntity}"/> /
/// <see cref="EntityDeletedEvent{TEntity}"/> events when the batch is large enough
/// that fan-out invalidation would be wasteful (e.g. a 100-row archive emitting
/// 100 cache eviction calls when one would suffice).
/// </summary>
/// <typeparam name="TEntity">The entity type. Must implement <see cref="IEmitEntityLifecycleEvents"/>.</typeparam>
/// <param name="Entities">
/// The affected rows (post-save state for create/update; pre-delete snapshot for
/// delete). Empty list is allowed but a no-op for invalidation.
/// </param>
/// <remarks>
/// <para>
/// Consumed by <c>RelationAggregateCacheInvalidator&lt;TRelated&gt;</c> alongside
/// the per-row events: a single bulk event drops the cached relation aggregates
/// for every parent of the relation in one <c>RemoveByTagAsync</c> call.
/// </para>
/// <para>
/// Hosts that emit this event SHOULD NOT also emit per-row events for the same
/// rows — the consumer treats them as alternatives, not complements. Mixing
/// causes duplicate invalidation work without correctness impact.
/// </para>
/// </remarks>
public sealed record EntityBulkUpdatedEvent<TEntity>(IReadOnlyList<TEntity> Entities) : IDomainEvent
    where TEntity : class, IEmitEntityLifecycleEvents;

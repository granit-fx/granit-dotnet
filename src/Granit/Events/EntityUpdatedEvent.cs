using Granit.Domain;

namespace Granit.Events;

/// <summary>
/// Domain event emitted automatically after an entity implementing
/// <see cref="IEmitEntityLifecycleEvents"/> is updated and successfully persisted.
/// </summary>
/// <typeparam name="TEntity">The entity type. Must implement <see cref="IEmitEntityLifecycleEvents"/>.</typeparam>
/// <param name="Entity">The updated entity instance (post-save state).</param>
/// <remarks>
/// Dispatched by <c>EntityLifecycleEventInterceptor</c> in <c>SavedChanges</c> (after commit).
/// <para>
/// For soft-deleted entities (<c>ISoftDeletable</c>) where <c>IsDeleted</c> transitions
/// from <c>false</c> to <c>true</c>, <see cref="EntityDeletedEvent{TEntity}"/> is emitted
/// instead of this event.
/// </para>
/// </remarks>
public sealed record EntityUpdatedEvent<TEntity>(TEntity Entity) : IDomainEvent
    where TEntity : class, IEmitEntityLifecycleEvents;

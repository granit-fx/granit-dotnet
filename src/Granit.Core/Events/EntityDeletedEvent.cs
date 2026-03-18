using Granit.Core.Domain;

namespace Granit.Core.Events;

/// <summary>
/// Domain event emitted automatically after an entity implementing
/// <see cref="IEmitEntityLifecycleEvents"/> is deleted and the change is successfully persisted.
/// </summary>
/// <typeparam name="TEntity">The entity type. Must implement <see cref="IEmitEntityLifecycleEvents"/>.</typeparam>
/// <param name="Entity">The deleted entity instance (pre-save snapshot captured before <c>SaveChanges</c>).</param>
/// <remarks>
/// Emitted in two cases:
/// <list type="bullet">
///   <item>EF Core state <c>Deleted</c> (hard delete).</item>
///   <item>EF Core state <c>Modified</c> where the entity implements <c>ISoftDeletable</c>
///   and <c>IsDeleted</c> transitions from <c>false</c> to <c>true</c> (soft delete).</item>
/// </list>
/// Dispatched by <c>EntityLifecycleEventInterceptor</c> in <c>SavedChanges</c> (after commit).
/// </remarks>
public sealed record EntityDeletedEvent<TEntity>(TEntity Entity) : IDomainEvent
    where TEntity : class, IEmitEntityLifecycleEvents;

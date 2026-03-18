using Granit.Core.Domain;

namespace Granit.Core.Events;

/// <summary>
/// Domain event emitted automatically after an entity implementing
/// <see cref="IEmitEntityLifecycleEvents"/> is successfully persisted for the first time.
/// </summary>
/// <typeparam name="TEntity">The entity type. Must implement <see cref="IEmitEntityLifecycleEvents"/>.</typeparam>
/// <param name="Entity">The created entity instance (post-save state).</param>
/// <remarks>
/// Dispatched by <c>EntityLifecycleEventInterceptor</c> in <c>SavedChanges</c> (after commit)
/// so that handlers can safely read the newly persisted data.
/// <para>
/// For distributed cross-service notification, see <c>EntityCreatedEto&lt;TEto&gt;</c>.
/// </para>
/// </remarks>
public sealed record EntityCreatedEvent<TEntity>(TEntity Entity) : IDomainEvent
    where TEntity : class, IEmitEntityLifecycleEvents;

namespace Granit.Core.Domain;

/// <summary>
/// Opt-in marker interface for automatic entity lifecycle event emission.
/// </summary>
/// <remarks>
/// Entities implementing this interface automatically emit
/// <see cref="Granit.Core.Events.EntityCreatedEvent{TEntity}"/>,
/// <see cref="Granit.Core.Events.EntityUpdatedEvent{TEntity}"/>, and
/// <see cref="Granit.Core.Events.EntityDeletedEvent{TEntity}"/> (all implementing
/// <see cref="Granit.Core.Events.IDomainEvent"/>) after <c>SaveChanges</c> commits,
/// via the <c>EntityLifecycleEventInterceptor</c>.
/// <para>
/// For distributed entity events across service boundaries, implement
/// <see cref="IHasEntityEto{TEto}"/> instead — it extends this interface automatically.
/// </para>
/// </remarks>
public interface IEmitEntityLifecycleEvents { }

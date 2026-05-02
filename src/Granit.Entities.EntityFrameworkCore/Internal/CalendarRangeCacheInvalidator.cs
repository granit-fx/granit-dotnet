using Granit.Entities.Endpoints.Internal;
using Granit.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Drops every cached calendar range for <typeparamref name="TEntity"/> when one of
/// its rows is created, updated, or deleted (story #1691). One closed-generic
/// instance is registered per entity that exposes a <c>CalendarLayoutDescriptor</c>,
/// keyed on the framework's local entity-lifecycle events
/// (<c>EntityCreatedEvent&lt;TEntity&gt;</c> / <c>EntityUpdatedEvent&lt;TEntity&gt;</c> /
/// <c>EntityDeletedEvent&lt;TEntity&gt;</c>) — the same events the
/// <c>EntityLifecycleEventInterceptor</c> emits during EF Core's <c>SavedChanges</c>.
/// </summary>
/// <remarks>
/// <para>
/// Per-entity invalidation tag (<see cref="CalendarRangeCacheKey.EvictionTag"/>) drops
/// every cached window for the entity in one call — finer-grained tagging would
/// require knowing which window the changed row falls into, which the cache layer
/// cannot determine without re-running the filter expression on the affected row.
/// </para>
/// <para>
/// Registered as <c>Scoped</c> per-entity because the lifecycle event itself is
/// dispatched in scope by the EF interceptor; <c>IFusionCache</c> is itself singleton
/// internally but the wrapper resolves correctly from any scope.
/// </para>
/// </remarks>
internal sealed class CalendarRangeCacheInvalidator<TEntity>(IFusionCache cache) :
    ILocalEventHandler<EntityCreatedEvent<TEntity>>,
    ILocalEventHandler<EntityUpdatedEvent<TEntity>>,
    ILocalEventHandler<EntityDeletedEvent<TEntity>>
    where TEntity : class, Granit.Domain.IEmitEntityLifecycleEvents
{
    private static readonly string EvictionTag =
        EntityCacheKey.EvictionTagForCalendarRange(typeof(TEntity).Name);

    public Task HandleAsync(EntityCreatedEvent<TEntity> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityUpdatedEvent<TEntity> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityDeletedEvent<TEntity> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    private async Task EvictAsync(CancellationToken cancellationToken) =>
        await cache.RemoveByTagAsync(EvictionTag, token: cancellationToken).ConfigureAwait(false);
}

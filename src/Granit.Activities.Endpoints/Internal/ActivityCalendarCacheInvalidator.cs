using Granit.Activities.Domain;
using Granit.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Activities.Endpoints.Internal;

/// <summary>
/// Drops the per-tenant activities calendar cache when an
/// <see cref="Activity"/> row is created, updated, or deleted (story #1801).
/// Mirror of the relation-aggregate / calendar-range invalidators shipped in
/// Phase 1.5 / D1 — same <see cref="ILocalEventHandler{TEvent}"/> triplet
/// pattern, same <c>RemoveByTagAsync</c> primitive.
/// </summary>
internal sealed class ActivityCalendarCacheInvalidator(IFusionCache cache) :
    ILocalEventHandler<EntityCreatedEvent<Activity>>,
    ILocalEventHandler<EntityUpdatedEvent<Activity>>,
    ILocalEventHandler<EntityDeletedEvent<Activity>>,
    ILocalEventHandler<EntityBulkUpdatedEvent<Activity>>
{
    public Task HandleAsync(EntityCreatedEvent<Activity> e, CancellationToken cancellationToken = default) =>
        EvictAsync(e.Entity.TenantId, cancellationToken);

    public Task HandleAsync(EntityUpdatedEvent<Activity> e, CancellationToken cancellationToken = default) =>
        EvictAsync(e.Entity.TenantId, cancellationToken);

    public Task HandleAsync(EntityDeletedEvent<Activity> e, CancellationToken cancellationToken = default) =>
        EvictAsync(e.Entity.TenantId, cancellationToken);

    public Task HandleAsync(EntityBulkUpdatedEvent<Activity> e, CancellationToken cancellationToken = default)
    {
        if (e.Entities.Count == 0)
        {
            return Task.CompletedTask;
        }
        // A bulk operation may span tenants — collapse the eviction to one
        // RemoveByTagAsync per distinct tenant rather than per row.
        return EvictBulkAsync(e.Entities, cancellationToken);
    }

    private async Task EvictAsync(Guid? tenantId, CancellationToken cancellationToken) =>
        await cache.RemoveByTagAsync(
            ActivityCalendarCacheKey.EvictionTag(tenantId),
            token: cancellationToken).ConfigureAwait(false);

    private async Task EvictBulkAsync(IReadOnlyList<Activity> activities, CancellationToken cancellationToken)
    {
        HashSet<Guid?> tenants = [.. activities.Select(a => a.TenantId).Distinct()];
        foreach (Guid? tenantId in tenants)
        {
            await cache.RemoveByTagAsync(
                ActivityCalendarCacheKey.EvictionTag(tenantId),
                token: cancellationToken).ConfigureAwait(false);
        }
    }
}

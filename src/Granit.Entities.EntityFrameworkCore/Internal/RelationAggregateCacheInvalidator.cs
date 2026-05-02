using Granit.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Drops every cached relation aggregate that depends on
/// <typeparamref name="TRelated"/> when one of its rows is created, updated,
/// or deleted (story #1793). Closes the loop on ADR-048: without this
/// invalidator, smart-button counters on the parent entity stay stale until
/// the FusionCache 30-second sliding TTL expires.
/// </summary>
/// <remarks>
/// <para>
/// One closed-generic instance is registered per related entity type that
/// participates in at least one relation declaration (intra-module
/// <c>HasMany</c> / <c>HasOne</c> on <see cref="EntityDefinitionBuilder{TEntity}"/>
/// or cross-module <c>IEntityRelationContributor</c> graft). The list of
/// per-relation eviction tags is captured at composition time inside the
/// injected <see cref="RelationAggregateInvalidationTargets{TRelated}"/>.
/// </para>
/// <para>
/// Granularity is per (source entity, relation name) — coarser than per
/// (source row id) because the framework's <see cref="RelationDescriptor"/>
/// does not yet carry an executable foreign-key predicate that would let us
/// extract the parent id from the changed related row. Coarse invalidation
/// drops the counters for every parent of the relation in one call; the
/// re-computation cost is the cache miss penalty on the next request.
/// </para>
/// </remarks>
internal sealed class RelationAggregateCacheInvalidator<TRelated>(
    IFusionCache cache,
    RelationAggregateInvalidationTargets<TRelated> targets) :
    ILocalEventHandler<EntityCreatedEvent<TRelated>>,
    ILocalEventHandler<EntityUpdatedEvent<TRelated>>,
    ILocalEventHandler<EntityDeletedEvent<TRelated>>,
    ILocalEventHandler<EntityBulkUpdatedEvent<TRelated>>
    where TRelated : class, Granit.Domain.IEmitEntityLifecycleEvents
{
    public Task HandleAsync(EntityCreatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityUpdatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityDeletedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    // One bulk event collapses to one eviction sweep — coarser tag granularity
    // already groups per (source, relation), so the bulk event simply avoids the
    // N-way fan-out cost when the host has a batch of writes (story #1794).
    public Task HandleAsync(EntityBulkUpdatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        localEvent.Entities.Count == 0
            ? Task.CompletedTask
            : EvictAsync(cancellationToken);

    private async Task EvictAsync(CancellationToken cancellationToken)
    {
        foreach (string tag in targets.EvictionTags)
        {
            await cache.RemoveByTagAsync(tag, token: cancellationToken).ConfigureAwait(false);
        }
    }
}

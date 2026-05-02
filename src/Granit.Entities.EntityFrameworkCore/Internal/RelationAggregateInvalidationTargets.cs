namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// DI-time payload captured per related entity type: the list of per-relation
/// eviction tags to drop when a row of <typeparamref name="TRelated"/> is created,
/// updated, or deleted (story #1793).
/// </summary>
/// <remarks>
/// One instance per <typeparamref name="TRelated"/>, registered as singleton by
/// <c>AddGranitEntitiesRelationAggregateInvalidation</c>. The list is fixed at
/// composition time — it captures every (source entity, relation name) pair that
/// declares <typeparamref name="TRelated"/> as the related type, across both
/// intra-module declarations and cross-module contributions.
/// </remarks>
internal sealed class RelationAggregateInvalidationTargets<TRelated>(IReadOnlyList<string> evictionTags)
    where TRelated : class
{
    public IReadOnlyList<string> EvictionTags { get; } = evictionTags;
}

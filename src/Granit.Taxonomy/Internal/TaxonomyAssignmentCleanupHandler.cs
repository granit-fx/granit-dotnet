using Granit.Domain;
using Granit.Events;
using Granit.Taxonomy.Registration;

namespace Granit.Taxonomy.Internal;

/// <summary>
/// Synchronous orphan cleanup (T5.1): when a taggable aggregate raises
/// <see cref="EntityDeletedEvent{TEntity}"/>, removes every <c>TagAssignment</c>
/// and <c>CategoryAssignment</c> row pointing at it.
/// </summary>
/// <remarks>
/// <para>
/// One closed-generic instance is registered per type passed to
/// <c>AddTaggableEntity&lt;TAggregate&gt;</c>. The handler resolves
/// <c>targetType</c> from <c>typeof(TEntity).FullName</c> — the same key
/// <c>AddTaggableEntity</c> uses to populate <see cref="TaggableTypeRegistry"/> —
/// so cleanup runs even when an aggregate is deleted by code paths that bypass
/// the assignment endpoints. The registry guard short-circuits when the type
/// is not registered, which keeps unrelated entity-delete events cheap.
/// </para>
/// <para>
/// Subscribed on the <em>local</em> event bus (in-process) rather than the
/// distributed bus: orphan rows live in the same database as the deleted
/// aggregate, so cross-service dispatch would only add latency.
/// </para>
/// </remarks>
internal sealed class TaxonomyAssignmentCleanupHandler<TEntity>(
    TaggableTypeRegistry registry,
    ITagAssignmentService tagAssignments,
    ICategoryAssignmentService categoryAssignments) :
    ILocalEventHandler<EntityDeletedEvent<TEntity>>
    where TEntity : Entity, IEmitEntityLifecycleEvents
{
    public async Task HandleAsync(
        EntityDeletedEvent<TEntity> localEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localEvent);

        string targetType = typeof(TEntity).FullName ?? typeof(TEntity).Name;
        if (!registry.IsRegistered(targetType))
        {
            return;
        }

        Guid targetId = localEvent.Entity.Id;

        await tagAssignments
            .RemoveAllAssignmentsAsync(targetType, targetId, cancellationToken)
            .ConfigureAwait(false);
        await categoryAssignments
            .RemoveAllAssignmentsAsync(targetType, targetId, cancellationToken)
            .ConfigureAwait(false);
    }
}

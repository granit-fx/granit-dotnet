using Granit.Domain;
using Granit.EntityMerge.Domain;

namespace Granit.EntityMerge;

/// <summary>
/// Orchestrates a merge of two instances of <typeparamref name="TAggregate"/>: validation,
/// concurrency control, dry-run preview, scalar-field application via
/// <c>IMergeable.MergeFrom</c>, scatter-gather across all registered
/// <see cref="IReferenceRewriter{TAggregate}"/>s, tombstone, audit, and outbox event.
/// </summary>
/// <remarks>
/// One concrete implementation lives in <c>Granit.EntityMerge.EntityFrameworkCore</c>
/// (<c>EfMergeService&lt;TAggregate&gt;</c>). Custom backends can be plugged in by
/// implementing this interface directly.
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root being merged.</typeparam>
public interface IMergeService<TAggregate>
    where TAggregate : Entity, IMergeable<TAggregate>
{
    /// <summary>
    /// Computes a dry-run preview: returns the field conflicts (with default winners) and
    /// the rewrite counts each registered <see cref="IReferenceRewriter{TAggregate}"/>
    /// would apply, without committing anything. Powers the admin merge wizard UI.
    /// </summary>
    Task<MergeResult<TAggregate>> MergePreviewAsync(
        Guid survivorId,
        Guid loserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a merge atomically. Throws <see cref="Granit.EntityMerge.Exceptions.MergeException"/> (422) when a hard
    /// invariant is violated, <see cref="Granit.Exceptions.EntityNotFoundException"/> (404) when a survivor/loser is
    /// missing, and <see cref="Granit.Exceptions.ConflictException"/> (409) when a survivor/loser is already merged or an
    /// idempotency key is reused with a different body. Matching idempotency-key replays return the cached result.
    /// </summary>
    Task<MergeResult<TAggregate>> MergeAsync(
        MergeRequest request,
        CancellationToken cancellationToken);
}

using Granit.Domain;
using Granit.Mergeable.Domain;

namespace Granit.Mergeable;

/// <summary>
/// Per-aggregate adapter the orchestrator delegates to for loading and persisting both ends
/// of a merge. Implemented by the consuming module (e.g. <c>Granit.Parties.Mergeable</c>
/// ships a <c>PartyMergeableAggregateAdapter</c>) so the framework <c>EfMergeService</c>
/// stays aggregate-agnostic.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root being merged.</typeparam>
public interface IMergeableAggregateAdapter<TAggregate>
    where TAggregate : Entity, IMergeable<TAggregate>
{
    /// <summary>
    /// Loads the aggregate, including tombstoned ones (the orchestrator must observe both
    /// sides of a merge — even if the loser is currently tombstoned by an earlier action).
    /// Implementations bypass the <see cref="IHasMergeTombstone"/> filter via
    /// <c>IDataFilter.Disable&lt;IHasMergeTombstone&gt;()</c> for the duration of the load.
    /// </summary>
    Task<TAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the survivor (with applied <c>MergeFrom</c> changes) and the loser (with
    /// tombstone set: <c>MergedIntoId</c> = survivor.Id, <c>MergedAt</c> = now). Both saves
    /// happen inside the orchestrator's <c>TransactionScope</c>, so EF tracking + outbox
    /// enrolment is automatic.
    /// </summary>
    Task PersistMergedPairAsync(TAggregate survivor, TAggregate loser, CancellationToken cancellationToken);

    /// <summary>
    /// Sets <see cref="IHasMergeTombstone.MergedIntoId"/> + <see cref="IHasMergeTombstone.MergedAt"/>
    /// on the loser. Tombstone columns have private setters on the aggregate; the consuming
    /// module exposes an internal mutation point (e.g. <c>internal void MarkAsMergedInto</c>)
    /// and routes through it here. The orchestrator never reaches inside the aggregate via
    /// reflection.
    /// </summary>
    void ApplyTombstone(TAggregate loser, Guid survivorId, DateTimeOffset mergedAt);

    /// <summary>
    /// Collapses chain merges (<c>A → B → C</c>): on the merge that produces a new survivor,
    /// the orchestrator calls this method to rewrite any earlier loser whose
    /// <c>MergedIntoId</c> still pointed at the previous survivor — guaranteeing a single
    /// hop is always enough for <c>ResolveCurrentAsync</c>. Returns the number of rows
    /// updated (typically 0 in a fresh merge, &gt; 0 when chain merges happen).
    /// </summary>
    Task<int> CollapseChainTombstonesAsync(Guid newSurvivorId, Guid oldSurvivorId, CancellationToken cancellationToken);
}

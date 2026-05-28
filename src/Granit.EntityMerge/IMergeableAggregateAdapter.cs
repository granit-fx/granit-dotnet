using Granit.Domain;
using Granit.EntityMerge.Domain;

namespace Granit.EntityMerge;

/// <summary>
/// Per-aggregate adapter the orchestrator delegates to for loading and persisting both ends
/// of a merge. Implemented by the consuming module (e.g. <c>Granit.Parties.EntityMerge</c>
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

    /// <summary>
    /// Stamps the <em>merged</em> domain event + integration event on the survivor in-memory
    /// (e.g. <c>Party.RaiseMergedEvents</c>) so the framework's
    /// <c>DomainEventDispatcherInterceptor</c> picks them up on the next <c>SaveChanges</c>
    /// — same transaction for the domain event, Wolverine outbox enrolment for the eto.
    /// Default no-op for adapters whose aggregate does not emit a merged-lifecycle event.
    /// Implementations MUST NOT touch the database; this hook only mutates the aggregate's
    /// pending event list.
    /// </summary>
    /// <param name="survivor">The survivor aggregate (already mutated by <c>MergeFrom</c>).</param>
    /// <param name="loser">The loser aggregate (already tombstoned by <see cref="ApplyTombstone"/>).</param>
    /// <param name="request">The original merge request — gives <c>Reason</c>, <c>Choices</c>, ids.</param>
    /// <param name="rewriteCounts">Counts produced by every registered <c>IReferenceRewriter&lt;TAggregate&gt;</c>,
    /// keyed by their <c>Description</c>. Adapters can extract aggregate-specific counts (e.g.
    /// <c>Party.ParentPartyId</c>) to populate richer event payloads.</param>
    /// <param name="mergedAt">Merge timestamp (orchestrator-provided <c>IClock.Now</c>).</param>
    void RaiseMergedEvents(
        TAggregate survivor,
        TAggregate loser,
        MergeRequest request,
        IReadOnlyDictionary<string, int> rewriteCounts,
        DateTimeOffset mergedAt)
    {
        // Default implementation: no event. Adapters opt in by overriding.
    }
}

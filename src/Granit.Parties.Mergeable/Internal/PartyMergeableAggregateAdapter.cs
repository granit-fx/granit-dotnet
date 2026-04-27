using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Parties.Mergeable.Internal;

/// <summary>
/// EF-Core-backed adapter wiring the <see cref="Party"/> aggregate into the generic merge
/// orchestrator. Bridges <c>EfMergeService&lt;Party&gt;</c> (in <c>Granit.Mergeable.EntityFrameworkCore</c>)
/// to the existing <see cref="PartiesDbContext"/> + <see cref="EfPartyStore"/> persistence.
/// </summary>
internal sealed class PartyMergeableAggregateAdapter(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IDataFilter dataFilter) : IMergeableAggregateAdapter<Party>
{
    /// <inheritdoc />
    public async Task<Party?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        // Bypass the IHasMergeTombstone filter — the orchestrator must observe both ends
        // of a merge even when the loser is already tombstoned by an earlier action.
        // Async-flow-scoped via IDataFilter; restored on dispose.
        using IDisposable bypass = dataFilter.Disable<IHasMergeTombstone>();

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.Parties
            .Include(p => p.Addresses)
            .Include(p => p.Emails)
            .Include(p => p.Phones)
            .Include(p => p.ExternalMappings)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PersistMergedPairAsync(
        Party survivor,
        Party loser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(survivor);
        ArgumentNullException.ThrowIfNull(loser);

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Both sides are loaded with separate DbContexts via LoadAsync above; reattach so
        // their changes flush in this single SaveChangesAsync inside the orchestrator's
        // ambient TransactionScope.
        //
        // NEVER call db.Parties.Update(party) here: Update cascades through the navigation
        // graph that LoadAsync fetched via Include(...) and marks every child (Address /
        // Email / Phone / ExternalMapping) as Modified, with the snapshot value of the
        // shadow FK PartyId = the original loserId. SaveChanges would then issue
        // `UPDATE … SET PartyId = loserId WHERE Id = childId` for every relocated child —
        // undoing the bulk SQL FK rewrite already performed by PartyChildrenReferenceRewriter
        // earlier in the same transaction. Children are owned exclusively by the bulk
        // rewriter; the adapter only persists scalar mutations on the aggregate root itself.
        AttachAsRootOnlyModified(db, survivor);
        AttachAsRootOnlyModified(db, loser);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AttachAsRootOnlyModified(PartiesDbContext db, Party party)
    {
        // Use ChangeTracker.TrackGraph to attach the in-memory graph: the root is forced
        // Modified (so AuditedEntityInterceptor stamps ModifiedAt/By), every navigation
        // child is forced Unchanged (no UPDATE issued) — the bulk rewriter is the sole
        // writer for the children tables.
        db.ChangeTracker.TrackGraph(party, node =>
        {
            EntityEntry entry = node.Entry;
            entry.State = ReferenceEquals(entry.Entity, party)
                ? EntityState.Modified
                : EntityState.Unchanged;
        });
    }

    /// <inheritdoc />
    public void ApplyTombstone(Party loser, Guid survivorId, DateTimeOffset mergedAt)
    {
        ArgumentNullException.ThrowIfNull(loser);
        loser.MarkAsMergedInto(survivorId, mergedAt);
    }

    /// <inheritdoc />
    public async Task<int> CollapseChainTombstonesAsync(
        Guid newSurvivorId,
        Guid oldSurvivorId,
        CancellationToken cancellationToken)
    {
        // For any party already tombstoned with MergedIntoId == oldSurvivor, retarget it
        // to newSurvivor so PartyId.ResolveCurrentAsync only ever needs one hop. Bulk SQL
        // UPDATE — never loaded into the change tracker. Bypass the IHasMergeTombstone
        // filter so we can target tombstoned rows.
        using IDisposable bypass = dataFilter.Disable<IHasMergeTombstone>();

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // ExecuteUpdateAsync bypasses ALL framework interceptors (audit, soft-delete,
        // domain-event dispatcher). That is intentional here: chain-collapse only retargets
        // an already-set tombstone pointer on rows that are themselves tombstoned and
        // hidden by the IHasMergeTombstone query filter — they are no longer part of the
        // user-visible audit timeline. ModifiedAt/By stay frozen at the original merge
        // (the only event a Party.MergedIntoId pointer change reflects is the orchestrator
        // walking the chain, never a domain mutation). Party does not implement
        // IConcurrencyAware, so there is no stamp to regenerate. The tombstone change
        // event is emitted by the orchestrator itself once per merge (not per hop), so
        // skipping the interceptor pipeline here is the correct semantic.
        //
        // The IHasMergeTombstone filter is already disabled by the IDataFilter scope above
        // — no need for a per-query IgnoreQueryFilters call.
        return await db.Parties
            .Where(p => p.MergedIntoId == oldSurvivorId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.MergedIntoId, newSurvivorId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

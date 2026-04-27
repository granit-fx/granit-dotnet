using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

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
        // ambient TransactionScope. Update() rather than Attach() because both have been
        // mutated (survivor: scalar field merge applied; loser: tombstone applied).
        db.Parties.Update(survivor);
        db.Parties.Update(loser);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

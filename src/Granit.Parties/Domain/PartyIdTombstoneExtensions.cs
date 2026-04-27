using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Domain;

/// <summary>
/// Tombstone follow-through for <see cref="PartyId"/> — resolves a (possibly stale) id to its
/// current survivor by reading the <see cref="IHasMergeTombstone.MergedIntoId"/> column on the
/// underlying <see cref="Party"/>. Used in the frontline of Wolverine handlers, scheduled
/// background jobs, webhook senders, and notification dispatchers — anywhere a
/// <see cref="PartyId"/> can survive a merge in a serialised payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Bulk-UPDATE rewriters (registered via
/// <c>IReferenceRewriter&lt;Party&gt;</c>) cover persisted columns like
/// <c>Invoice.PartyId</c>, <c>Subscription.PartyId</c>, etc. They do <em>not</em> cover
/// <see cref="PartyId"/> values living inside JSON payloads of in-flight Wolverine messages,
/// scheduled jobs, queued webhooks, or pending notifications. The follow-through lets every
/// such consumer transparently route to the survivor without any out-of-band reconciliation.
/// </para>
/// <para>
/// <b>Single-hop guarantee.</b> The merge orchestrator collapses chain merges
/// (<c>A → B → C</c>) at merge time by running
/// <c>UPDATE parties SET MergedIntoId = newSurvivor WHERE MergedIntoId = oldSurvivor</c>
/// in the same transaction as the new merge. Therefore one hop is always enough — this
/// extension never needs to recurse.
/// </para>
/// <para>
/// <b>Filter bypass.</b> The standard <see cref="IHasMergeTombstone"/> query filter hides
/// tombstoned rows. <see cref="ResolveCurrentAsync"/> disables the filter for the duration
/// of the lookup so the loser row remains readable. The disable is async-flow-scoped via
/// <see cref="IDataFilter"/>, restored on dispose.
/// </para>
/// <para>
/// <b>Frontline call sites.</b> Every Wolverine handler / background job that takes a
/// <see cref="PartyId"/> in its payload should call
/// <see cref="ResolveCurrentAsync(PartyId, IPartyReader, IDataFilter, CancellationToken)"/>
/// in its first statement. Endpoints accepting a <c>partyId</c> route param can either
/// resolve transparently or return <c>308 Permanent Redirect</c> to the survivor URL.
/// </para>
/// </remarks>
public static class PartyIdTombstoneExtensions
{
    /// <summary>
    /// Resolves <paramref name="partyId"/> to its current survivor via the tombstone, or
    /// returns the original id when the party is alive (no tombstone) or no longer exists.
    /// </summary>
    /// <param name="partyId">The (possibly stale) party id carried in a serialised payload.</param>
    /// <param name="reader">Party reader used to look the row up.</param>
    /// <param name="dataFilter">Data filter — required so the lookup can transparently
    /// bypass the <see cref="IHasMergeTombstone"/> query filter and observe the tombstone.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The survivor id when the party has been merged out, otherwise <paramref name="partyId"/>.</returns>
    public static async ValueTask<PartyId> ResolveCurrentAsync(
        this PartyId partyId,
        IPartyReader reader,
        IDataFilter dataFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partyId);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(dataFilter);

        // Disable the IHasMergeTombstone query filter so the lookup observes tombstoned rows.
        // Restored on dispose; async-flow-scoped via IDataFilter, no other queries on this
        // flow are affected outside the using block.
        using IDisposable bypass = dataFilter.Disable<IHasMergeTombstone>();

        Party? party = await reader.GetByIdAsync(partyId, cancellationToken).ConfigureAwait(false);
        return party?.MergedIntoId is { } survivor
            ? PartyId.Create(survivor)
            : partyId;
    }
}

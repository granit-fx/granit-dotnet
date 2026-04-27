using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Mergeable.Internal;

/// <summary>
/// Re-parents <see cref="Party"/> children of the loser onto the survivor — i.e. parties
/// whose <see cref="Party.ParentContactId"/> equals the loser's id are redirected to the
/// survivor. Implemented as a SQL bulk-update so the change tracker is not loaded with
/// potentially thousands of rows and to avoid the EF shadow-FK pitfall.
/// </summary>
/// <remarks>
/// <para>
/// The query disables the <see cref="IHasMergeTombstone"/> filter for its scope so that
/// already-tombstoned children (rare but possible after chain merges) are still rewritten
/// to point at the latest survivor — guaranteeing the single-hop invariant relied on by
/// <c>PartyId.ResolveCurrentAsync</c>.
/// </para>
/// </remarks>
internal sealed class PartyParentReferenceRewriter(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IDataFilter dataFilter) : IReferenceRewriter<Party>
{
    /// <summary>
    /// Stable description string published as the rewrite-counts map key. Adapters route on
    /// this constant to extract the reparented-children count for downstream events.
    /// </summary>
    internal const string RewriterDescription = "Party.ParentContactId";

    /// <inheritdoc />
    public string Description => RewriterDescription;

    /// <inheritdoc />
    public async Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        using IDisposable _ = dataFilter.Disable<IHasMergeTombstone>();

        var loserPartyId = PartyId.Create(loserId);
        var survivorPartyId = PartyId.Create(survivorId);

        return await db.Parties
            .Where(p => p.ParentContactId == loserPartyId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentContactId, survivorPartyId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        using IDisposable _ = dataFilter.Disable<IHasMergeTombstone>();

        var loserPartyId = PartyId.Create(loserId);

        return await db.Parties
            .Where(p => p.ParentContactId == loserPartyId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
